using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using SystemMonitor.Shared.Models;

using SharedLogLevel = SystemMonitor.Shared.Models.LogLevel;

namespace SystemMonitor.Worker.Workers;

public class LogProcessorWorker : BackgroundService
{
    private readonly ServiceBusClient _sbClient;
    private readonly CosmosClient _cosmos;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<LogProcessorWorker> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public LogProcessorWorker(
        ServiceBusClient sbClient,
        CosmosClient cosmos,
        IHttpClientFactory httpFactory,
        IConfiguration cfg,
        ILogger<LogProcessorWorker> logger)
    {
        _sbClient = sbClient;
        _cosmos = cosmos;
        _httpFactory = httpFactory;
        _cfg = cfg;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var logsContainer = _cosmos.GetDatabase(_cfg["CosmosDb:Database"])
                                        .GetContainer("logs");
        var anomalyContainer = _cosmos.GetDatabase(_cfg["CosmosDb:Database"])
                                        .GetContainer("anomalies");

        await using var processor = _sbClient.CreateProcessor(
            "logs-queue",
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 4,
                AutoCompleteMessages = false
            });

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var batch = JsonSerializer.Deserialize<IngestionBatch<LogEvent>>(
                    args.Message.Body.ToArray(), JsonOpts)!;

                var tasks = batch.Events.Select(e =>
                    logsContainer.UpsertItemAsync(e, new PartitionKey(e.AgentId),
                        cancellationToken: stoppingToken));
                await Task.WhenAll(tasks);

                var errors = batch.Events.Count(e =>
                    e.Level is SharedLogLevel.Error or SharedLogLevel.Critical);

                if (errors > 10)
                {
                    var anomaly = new AnomalyResult
                    {
                        SourceEventId = batch.Events.First().Id,
                        AgentId = batch.Events.First().AgentId,
                        HostName = batch.Events.First().HostName,
                        IsAnomaly = true,
                        Confidence = Math.Min(1.0, errors / 20.0),
                        Type = AnomalyType.Spike,
                        Severity = errors > 50
                            ? AnomalySeverity.Critical
                            : AnomalySeverity.High,
                        Description =
                            $"Log error spike: {errors} errors in one batch",
                        AffectedMetrics = new Dictionary<string, double>
                        { ["error_count"] = errors }
                    };

                    await anomalyContainer.UpsertItemAsync(
                        anomaly, new PartitionKey(anomaly.AgentId),
                        cancellationToken: stoppingToken);

                    _logger.LogWarning(
                        "Log spike anomaly for {AgentId}: {Count} errors",
                        anomaly.AgentId, errors);
                }

                await args.CompleteMessageAsync(args.Message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing log batch");
                await args.AbandonMessageAsync(args.Message,
                    cancellationToken: stoppingToken);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Log queue Service Bus error");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        await processor.StopProcessingAsync();
    }
}