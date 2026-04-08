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
    private readonly Random _rng = new();

    private static readonly (string raw, string resolved, string tier)[] _driftScenarios =
    [
        ("sys_name",                "system_name",           "Fuzzy"),
        ("sys_identifier",          "system_name",           "Alias"),
        ("host_label",              "system_name",           "OpenAI"),
        ("device_label",            "system_name",           "Fuzzy"),
        ("fw_hostname",             "system_name",           "OpenAI"),
        ("hostname_str",            "system_name",           "Fuzzy"),
        ("devName",                 "system_name",           "Alias"),
        ("node_identifier",         "system_name",           "OpenAI"),

        ("cpu_usage_pct",           "cpu_percent",           "Fuzzy"),
        ("processor_utilization",   "cpu_percent",           "Fuzzy"),
        ("cpu_util",                "cpu_percent",           "Alias"),
        ("cpuUsage",                "cpu_percent",           "Fuzzy"),

        ("mem_used_percent",        "memory_percent",        "Fuzzy"),
        ("memory_utilization",      "memory_percent",        "Fuzzy"),
        ("ram_usage_pct",           "memory_percent",        "Alias"),
        ("memUsedPct",              "memory_percent",        "Fuzzy"),

        ("interface_bytes_in",      "network_in_mbps",       "OpenAI"),
        ("rx_mbps",                 "network_in_mbps",       "Alias"),
        ("bytes_received",          "network_in_mbps",       "OpenAI"),
        ("net_rx_rate",             "network_in_mbps",       "Fuzzy"),
        ("tx_mbps",                 "network_out_mbps",      "Alias"),
        ("bytes_transmitted",       "network_out_mbps",      "OpenAI"),

        ("disk_io_write",           "disk_write_mbps",       "Fuzzy"),
        ("write_throughput_mbps",   "disk_write_mbps",       "Fuzzy"),
        ("disk_write_rate",         "disk_write_mbps",       "Alias"),
        ("read_throughput_mbps",    "disk_read_mbps",        "Fuzzy"),

        ("resp_time_p99",           "p99_latency_ms",        "Alias"),
        ("response_time_99th",      "p99_latency_ms",        "Fuzzy"),
        ("latency_99p",             "p99_latency_ms",        "Fuzzy"),
        ("p99_response_ms",         "p99_latency_ms",        "Alias"),

        ("fortios.user",            "user.name",             "Alias"),
        ("src_user",                "user.name",             "Alias"),
        ("auth_user",               "user.name",             "Fuzzy"),
        ("username_str",            "user.name",             "Fuzzy"),

        ("pkt_count",               "sessions_allowed",      "OpenAI"),
        ("blocked_count",           "sessions_blocked",      "Alias"),
        ("deny_pkt",                "sessions_blocked",      "Fuzzy"),
        ("ips_alert_count",         "intrusion_attempts",    "Alias"),
        ("threat_count",            "intrusion_attempts",    "OpenAI"),

        ("active_conn",             "active_connections",    "Fuzzy"),
        ("conn_count",              "active_connections",    "Alias"),
        ("query_exec_time",         "query_time_ms",         "Fuzzy"),
        ("repl_delay_ms",           "replication_lag_ms",    "Alias"),

        ("unknown_field_x47",       null,                      "Unresolved"),
        ("fgt_custom_v2",           null,                      "Unresolved"),
        ("_sys_internal_ref",       null,                      "Unresolved"),
        ("metric_v3_compat",        null,                      "Unresolved"),
    ];

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
            await _hub.Clients.All.SendAsync("BroadcastAnomaly", anomaly, ct);
        }

        if (_rng.NextDouble() < 0.28)
        {
            var evt = SimulateSchemaEvent(batch.Events.FirstOrDefault());
            if (evt != null)
            {
                _cache.RecordSchemaEvent(evt);
                await _hub.Clients.All.SendAsync("SchemaEventDetected", evt, ct);
                _logger.LogInformation(
                    "[Schema] {Agent} — {Raw} → {Resolved} ({Confidence:P0})",
                    evt.AgentId, evt.RawAttribute,
                    evt.ResolvedAttribute ?? "UNRESOLVED", evt.Confidence);
            }
        }

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
                await Task.WhenAll(records.Select(r =>
                    container.UpsertItemAsync(r,
                        new PartitionKey(r.Timestamp.ToString("O")))));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cosmos write failed for training data");
            }
        }, ct);

        await Task.CompletedTask;
    }

    private SchemaResolutionEvent? SimulateSchemaEvent(MetricEvent? source)
    {
        if (source == null) return null;

        var scenario = _driftScenarios[_rng.Next(_driftScenarios.Length)];
        var wasResolved = scenario.resolved != null;
        var confidence = wasResolved
            ? Math.Round(0.35 + _rng.NextDouble() * 0.60, 2)
            : 0.0;

        return new SchemaResolutionEvent
        {
            AgentId = source.AgentId,
            HostName = source.HostName,
            RawAttribute = scenario.raw,
            ResolvedAttribute = scenario.resolved,
            Confidence = confidence,
            ResolutionTier = scenario.tier,
            WasResolved = wasResolved,
        };
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
            ["disk_percent"] = (85, 95),
            ["query_time_ms"] = (500, 2000),
            ["replication_lag_ms"] = (1000, 5000),
            ["active_connections"] = (300, 450),
            ["intrusion_attempts"] = (50, 150),
            ["sessions_blocked"] = (300, 600),
            ["cache_hit_rate"] = (70, 50),  // inverted — low is bad
            ["evictions_per_sec"] = (200, 500),
            ["handle_count"] = (5000, 8000),
            ["page_faults_per_sec"] = (3000, 6000),
            ["deadlocks"] = (5, 15),
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
        string containerName, IReadOnlyList<T> items,
        Func<T, string> partitionKeySelector, CancellationToken ct)
    {
        return Task.Run(async () =>
        {
            try
            {
                var container = _cosmos
                    .GetDatabase(_cfg["CosmosDb:Database"])
                    .GetContainer(containerName);
                await Task.WhenAll(items.Select(item =>
                    container.UpsertItemAsync(item,
                        new PartitionKey(partitionKeySelector(item)))));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Background Cosmos write failed for {Container}", containerName);
            }
        }, ct);
    }
}