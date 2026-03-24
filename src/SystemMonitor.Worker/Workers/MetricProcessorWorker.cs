using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Worker.Workers;

public class MetricProcessorWorker : BackgroundService
{
    private readonly ServiceBusClient _sbClient;
    private readonly CosmosClient _cosmos;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<MetricProcessorWorker> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public MetricProcessorWorker(
        ServiceBusClient sbClient,
        CosmosClient cosmos,
        IHttpClientFactory httpFactory,
        IConfiguration cfg,
        ILogger<MetricProcessorWorker> logger)
    {
        _sbClient = sbClient;
        _cosmos = cosmos;
        _httpFactory = httpFactory;
        _cfg = cfg;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var container = _cosmos.GetDatabase(_cfg["CosmosDb:Database"])
                                        .GetContainer("metrics");
        var anomalyContainer = _cosmos.GetDatabase(_cfg["CosmosDb:Database"])
                                        .GetContainer("anomalies");

        var hubConnection = new HubConnectionBuilder()
            .WithUrl(_cfg["SignalR:HubUrl"]!)
            .WithAutomaticReconnect()
            .Build();

        try { await hubConnection.StartAsync(stoppingToken); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not connect to SignalR — will retry");
        }

        await using var processor = _sbClient.CreateProcessor(
            "metrics-queue",
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 8,
                AutoCompleteMessages = false,
                MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
            });

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var batch = JsonSerializer.Deserialize<IngestionBatch<MetricEvent>>(
                    args.Message.Body.ToArray(), JsonOpts)!;

                var tasks = batch.Events.Select(e =>
                    container.UpsertItemAsync(e, new PartitionKey(e.AgentId),
                        cancellationToken: stoppingToken));
                await Task.WhenAll(tasks);

                var http = _httpFactory.CreateClient("AiService");
                var payload = JsonSerializer.Serialize(batch.Events, JsonOpts);
                var response = await http.PostAsync(
                    "/analyze-metrics",
                    new StringContent(payload, System.Text.Encoding.UTF8,
                        "application/json"),
                    stoppingToken);

                if (response.IsSuccessStatusCode)
                {
                    var results = await response.Content
                        .ReadFromJsonAsync<List<AnomalyResult>>(JsonOpts,
                            cancellationToken: stoppingToken)
                        ?? new();

                    foreach (var anomaly in results.Where(r => r.IsAnomaly))
                    {
                        await anomalyContainer.UpsertItemAsync(
                            anomaly, new PartitionKey(anomaly.AgentId),
                            cancellationToken: stoppingToken);

                        if (hubConnection.State == HubConnectionState.Connected)
                            await hubConnection.InvokeAsync(
                                "BroadcastAnomaly", anomaly, stoppingToken);

                        _logger.LogWarning(
                            "Anomaly: {Agent} {Type} {Severity} ({Confidence:P0})",
                            anomaly.AgentId, anomaly.Type,
                            anomaly.Severity, anomaly.Confidence);
                    }
                }

                await args.CompleteMessageAsync(args.Message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing metric batch");
                await args.AbandonMessageAsync(args.Message,
                    cancellationToken: stoppingToken);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception,
                "Service Bus error on {EntityPath}", args.EntityPath);
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        await processor.StopProcessingAsync();
        await hubConnection.StopAsync();
    }
}