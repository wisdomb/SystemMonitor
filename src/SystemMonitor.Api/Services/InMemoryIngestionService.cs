using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SystemMonitor.Api.Hubs;
using SystemMonitor.Shared.Models;
using SharedLogLevel = SystemMonitor.Shared.Models.LogLevel;

namespace SystemMonitor.Api.Services;

public class InMemoryIngestionService : IIngestionService
{
    private readonly CosmosClient _cosmos;
    private readonly IConfiguration _cfg;
    private readonly TelemetryCache _cache;
    private readonly IHubContext<MonitoringHub> _hub;
    private readonly ILogger<InMemoryIngestionService> _logger;

    public InMemoryIngestionService(
        CosmosClient cosmos,
        IConfiguration cfg,
        TelemetryCache cache,
        IHubContext<MonitoringHub> hub,
        ILogger<InMemoryIngestionService> logger)
    {
        _cosmos = cosmos;
        _cfg = cfg;
        _cache = cache;
        _hub = hub;
        _logger = logger;
    }

    public async Task EnqueueMetricsAsync(
        IngestionBatch<MetricEvent> batch, CancellationToken ct)
    {
        _cache.RecordMetrics(batch.Events);

        var anomalies = DetectAnomalies(batch.Events);
        foreach (var anomaly in anomalies)
        {
            _cache.RecordAnomaly(anomaly);
            await _hub.Clients.All.SendAsync(
                "BroadcastAnomaly", anomaly, ct);

            _logger.LogWarning(
                "[Anomaly] {AgentId} {Description} ({Confidence:P0})",
                anomaly.AgentId, anomaly.Description, anomaly.Confidence);
        }

        if (anomalies.Count > 0)
            _cache.AddActivity(
                $"{anomalies.Count} anomaly(s) from {batch.Events.FirstOrDefault()?.AgentId}",
                "warn");
        else
            _cache.AddActivity(
                $"Received {batch.Events.Count} metrics from {batch.Events.FirstOrDefault()?.AgentId}",
                "info");

        _ = PersistAsync("metrics", batch.Events, e => e.AgentId, ct);
        await Task.CompletedTask;
    }

    public async Task EnqueueLogsAsync(
        IngestionBatch<LogEvent> batch, CancellationToken ct)
    {
        _cache.RecordLogs(batch.Events);

        var errorCount = batch.Events.Count(e =>
            e.Level is SharedLogLevel.Error or SharedLogLevel.Critical);
        if (errorCount > 0)
            _cache.AddActivity(
                $"{errorCount} error/critical logs from {batch.Events.FirstOrDefault()?.AgentId}",
                "warn");

        _ = PersistAsync("logs", batch.Events, e => e.AgentId, ct);
        await Task.CompletedTask;
    }

    public async Task EnqueueTrainingDataAsync(
        IList<TrainingDataRecord> records, CancellationToken ct)
    {
        _cache.AddActivity(
            $"Training data uploaded: {records.Count} records queued", "success");

        _ = Task.Run(async () =>
        {
            try
            {
                var container = _cosmos
                    .GetDatabase(_cfg["CosmosDb:Database"])
                    .GetContainer("trainingData");
                var tasks = records.Select(r =>
                    container.UpsertItemAsync(r,
                        new PartitionKey(r.Timestamp.ToString("O"))));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cosmos write failed for training data");
            }
        }, ct);

        await Task.CompletedTask;
    }

    private List<AnomalyResult> DetectAnomalies(IReadOnlyList<MetricEvent> events)
    {
        var results = new List<AnomalyResult>();
        if (events.Count == 0) return results;

        var thresholds = new Dictionary<string, (double warn, double critical)>
        {
            ["cpu_percent"] = (85, 95),
            ["memory_percent"] = (85, 95),
            ["error_rate"] = (0.10, 0.25),
            ["p99_latency_ms"] = (500, 1000),
            ["disk_write_mbps"] = (80, 100),
        };

        foreach (var evt in events)
        {
            foreach (var (key, (warn, critical)) in thresholds)
            {
                if (!evt.Values.TryGetValue(key, out var value)) continue;

                AnomalySeverity? severity = null;
                double confidence = 0;

                if (value >= critical)
                {
                    severity = AnomalySeverity.Critical;
                    confidence = Math.Min(1.0, 0.90 + (value - critical) / critical * 0.5);
                }
                else if (value >= warn)
                {
                    severity = AnomalySeverity.High;
                    confidence = 0.75 + (value - warn) / (critical - warn) * 0.15;
                }

                if (severity.HasValue)
                {
                    results.Add(new AnomalyResult
                    {
                        SourceEventId = evt.Id,
                        AgentId = evt.AgentId,
                        HostName = evt.HostName,
                        IsAnomaly = true,
                        Confidence = confidence,
                        Type = AnomalyType.Spike,
                        Severity = severity.Value,
                        DetectedAt = DateTimeOffset.UtcNow,
                        Description = $"{key} spike: {value:F1} (threshold {warn})",
                        AffectedMetrics = new Dictionary<string, double> { [key] = value }
                    });
                    break;
                }
            }
        }

        return results;
    }

    private Task PersistAsync<T>(
        string containerName,
        IReadOnlyList<T> items,
        Func<T, string> partitionKeySelector,
        CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            try
            {
                var container = _cosmos
                    .GetDatabase(_cfg["CosmosDb:Database"])
                    .GetContainer(containerName);
                var tasks = items.Select(item =>
                    container.UpsertItemAsync(item,
                        new PartitionKey(partitionKeySelector(item))));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex,
                    "Background Cosmos write failed for {Container}", containerName);
            }
        }, ct);
    }
}