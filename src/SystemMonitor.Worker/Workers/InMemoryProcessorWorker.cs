using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using SystemMonitor.Shared;
using SystemMonitor.Shared.Models;

using SharedLogLevel = SystemMonitor.Shared.Models.LogLevel;

namespace SystemMonitor.Worker.Workers;

public class InMemoryProcessorWorker : BackgroundService
{
    private readonly CosmosClient _cosmos;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<InMemoryProcessorWorker> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public InMemoryProcessorWorker(
        CosmosClient cosmos,
        IHttpClientFactory httpFactory,
        IConfiguration cfg,
        ILogger<InMemoryProcessorWorker> logger)
    {
        _cosmos = cosmos;
        _httpFactory = httpFactory;
        _cfg = cfg;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "[InMemory] Worker started — reading from in-memory channels");

        var db = _cosmos.GetDatabase(_cfg["CosmosDb:Database"]);
        var metricsContainer = db.GetContainer("metrics");
        var logsContainer = db.GetContainer("logs");
        var anomalyContainer = db.GetContainer("anomalies");

        await Task.WhenAll(
            ConsumeMetricsAsync(metricsContainer, anomalyContainer, stoppingToken),
            ConsumeLogsAsync(logsContainer, stoppingToken),
            ConsumeTrainingAsync(stoppingToken)
        );
    }

    private async Task ConsumeMetricsAsync(
        Container metrics, Container anomalies, CancellationToken ct)
    {
        await foreach (var batch in
            InMemoryQueues.Metrics.Reader.ReadAllAsync(ct))
        {
            try
            {
                var tasks = batch.Events.Select(e =>
                    metrics.UpsertItemAsync(e, new PartitionKey(e.AgentId),
                        cancellationToken: ct));
                await Task.WhenAll(tasks);

                var http = _httpFactory.CreateClient("AiService");
                var payload = JsonSerializer.Serialize(batch.Events, JsonOpts);
                var response = await http.PostAsync(
                    "/analyze-metrics",
                    new StringContent(payload, System.Text.Encoding.UTF8,
                        "application/json"),
                    ct);

                if (response.IsSuccessStatusCode)
                {
                    var results = await response.Content
                        .ReadFromJsonAsync<List<AnomalyResult>>(JsonOpts,
                            cancellationToken: ct)
                        ?? new();

                    foreach (var anomaly in results.Where(r => r.IsAnomaly))
                    {
                        await anomalies.UpsertItemAsync(
                            anomaly, new PartitionKey(anomaly.AgentId),
                            cancellationToken: ct);

                        _logger.LogWarning(
                            "[InMemory] Anomaly: {Agent} {Type} {Severity} " +
                            "({Confidence:P0})",
                            anomaly.AgentId, anomaly.Type,
                            anomaly.Severity, anomaly.Confidence);
                    }
                }

                _logger.LogDebug(
                    "[InMemory] Processed {Count} metrics", batch.Events.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[InMemory] Error processing metric batch");
            }
        }
    }

    private async Task ConsumeLogsAsync(Container logs, CancellationToken ct)
    {
        await foreach (var batch in InMemoryQueues.Logs.Reader.ReadAllAsync(ct))
        {
            try
            {
                var tasks = batch.Events.Select(e =>
                    logs.UpsertItemAsync(e, new PartitionKey(e.AgentId),
                        cancellationToken: ct));
                await Task.WhenAll(tasks);

                var errors = batch.Events.Count(e =>
                    e.Level is SharedLogLevel.Error or SharedLogLevel.Critical);

                if (errors > 10)
                    _logger.LogWarning(
                        "[InMemory] Log spike: {Count} errors from {Agent}",
                        errors, batch.Events.FirstOrDefault()?.AgentId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[InMemory] Error processing log batch");
            }
        }
    }

    private async Task ConsumeTrainingAsync(CancellationToken ct)
    {
        await foreach (var records in
            InMemoryQueues.Training.Reader.ReadAllAsync(ct))
        {
            try
            {
                var http = _httpFactory.CreateClient("AiService");
                var payload = JsonSerializer.Serialize(records, JsonOpts);
                var response = await http.PostAsync(
                    "/train",
                    new StringContent(payload, System.Text.Encoding.UTF8,
                        "application/json"),
                    ct);

                if (response.IsSuccessStatusCode)
                    _logger.LogInformation(
                        "[InMemory] Training triggered with {Count} records",
                        records.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "[InMemory] Training failed");
            }
        }
    }
}