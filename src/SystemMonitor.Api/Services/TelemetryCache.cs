using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Services;

public class TelemetryCache
{
    private readonly object _lock = new();

    private readonly Dictionary<string, AgentSnapshot> _agents = new();

    private readonly Queue<LogEvent> _recentLogs = new();
    private readonly Queue<SchemaResolutionEvent> _schemaEvents = new();
    private const int MaxLogs = 500;

    private readonly Queue<ActivityEntry> _activityLog = new();
    private const int MaxActivity = 200;

    public void RecordMetrics(IReadOnlyList<MetricEvent> events, string tenantId = "")
    {
        lock (_lock)
        {
            foreach (var e in events)
            {
                var key = e.AgentId;
                if (!_agents.TryGetValue(key, out var snap))
                {
                    snap = new AgentSnapshot(e.AgentId, e.HostName, tenantId);
                    _agents[key] = snap;
                }
                snap.AddMetric(e);
            }
        }
    }

    public Dictionary<string, double> GetHealthScoresForTenant(string tenantId)
    {
        lock (_lock)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-5);
            return _agents.Values
                .Where(a => a.TenantId == tenantId && a.LastSeen >= cutoff)
                .ToDictionary(a => a.AgentId, a => a.HealthScore);
        }
    }

    public DashboardSnapshot GetSnapshotForTenant(string tenantId)
    {
        lock (_lock)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-5);
            var agents = _agents.Values
                .Where(a => a.TenantId == tenantId && a.LastSeen >= cutoff)
                .ToList();

            var allAnomalies = agents
                .SelectMany(a => a.RecentAnomalies)
                .Where(a => a.DetectedAt >= DateTimeOffset.UtcNow.AddHours(-1))
                .OrderByDescending(a => a.DetectedAt)
                .ToList();

            return new DashboardSnapshot
            {
                Agents = agents.ToList(),
                RecentAnomalies = allAnomalies,
                TotalAgents = agents.Count,
                ActiveAgents = agents.Count,
                AnomaliesLast1h = allAnomalies.Count,
                CriticalAlerts = allAnomalies.Count(a => a.Severity == AnomalySeverity.Critical),
                AvgHealthScore = agents.Any() ? agents.Average(a => a.HealthScore) : 100,
                IngestionRate = agents.Sum(a => a.RecentMetricCount)
            };
        }
    }

    public void RecordLogs(IReadOnlyList<LogEvent> events)
    {
        lock (_lock)
        {
            foreach (var e in events)
            {
                _recentLogs.Enqueue(e);
                while (_recentLogs.Count > MaxLogs) _recentLogs.Dequeue();
            }
        }
    }

    public void RecordSchemaEvent(SchemaResolutionEvent evt)
    {
        lock (_lock)
        {
            _schemaEvents.Enqueue(evt);
            while (_schemaEvents.Count > 200) _schemaEvents.Dequeue();
        }
    }

    public List<SchemaResolutionEvent> GetSchemaEvents(int limit = 100)
    {
        lock (_lock)
            return _schemaEvents.TakeLast(limit).OrderByDescending(e => e.DetectedAt).ToList();
    }

    public void RecordAnomaly(AnomalyResult anomaly)
    {
        lock (_lock)
        {
            if (_agents.TryGetValue(anomaly.AgentId, out var snap))
                snap.AddAnomaly(anomaly);
        }
    }

    public void AddActivity(string message, string level = "info")
    {
        lock (_lock)
        {
            _activityLog.Enqueue(new ActivityEntry
            {
                Timestamp = DateTimeOffset.UtcNow,
                Message = message,
                Level = level
            });
            while (_activityLog.Count > MaxActivity) _activityLog.Dequeue();
        }
    }

    public DashboardSnapshot GetSnapshot()
    {
        lock (_lock)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-5);
            var agents = _agents.Values
                .Where(a => a.LastSeen >= cutoff)
                .ToList();

            var allAnomalies = agents
                .SelectMany(a => a.RecentAnomalies)
                .Where(a => a.DetectedAt >= DateTimeOffset.UtcNow.AddHours(-1))
                .OrderByDescending(a => a.DetectedAt)
                .ToList();

            return new DashboardSnapshot
            {
                Agents = agents.ToList(),
                RecentAnomalies = allAnomalies,
                TotalAgents = agents.Count,
                ActiveAgents = agents.Count,
                AnomaliesLast1h = allAnomalies.Count,
                CriticalAlerts = allAnomalies.Count(a =>
                    a.Severity == AnomalySeverity.Critical),
                AvgHealthScore = agents.Any() ? agents.Average(a => a.HealthScore) : 100,
                IngestionRate = agents.Sum(a => a.RecentMetricCount)
            };
        }
    }

    public Dictionary<string, double> GetHealthScores()
    {
        lock (_lock)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-5);
            return _agents.Values
                .Where(a => a.LastSeen >= cutoff)
                .ToDictionary(a => a.AgentId, a => a.HealthScore);
        }
    }

    public List<TimeSeriesPoint> GetTimeSeries(
        string agentId, string metricKey, int windowMinutes)
    {
        lock (_lock)
        {
            if (!_agents.TryGetValue(agentId, out var snap))
                return new();

            var since = DateTimeOffset.UtcNow.AddMinutes(-windowMinutes);
            return snap.GetTimeSeries(metricKey, since);
        }
    }

    public List<string> GetActiveAgentIds()
    {
        lock (_lock)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-10);
            return _agents.Values
                .Where(a => a.LastSeen >= cutoff)
                .Select(a => a.AgentId)
                .ToList();
        }
    }

    public List<LogEvent> GetRecentLogs(int limit = 200)
    {
        lock (_lock)
        {
            return _recentLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(limit)
                .ToList();
        }
    }

    public List<ActivityEntry> GetActivityLog(int limit = 100)
    {
        lock (_lock)
        {
            return _activityLog
                .OrderByDescending(a => a.Timestamp)
                .Take(limit)
                .ToList();
        }
    }
}

public class AgentSnapshot
{
    public string AgentId { get; }
    public string HostName { get; }
    public DateTimeOffset LastSeen { get; private set; }
    private readonly Dictionary<string, Queue<(DateTimeOffset ts, double val)>> _series = new();
    private const int MaxSeriesPoints = 200;

    private readonly Queue<AnomalyResult> _anomalies = new();

    public string TenantId { get; }

    public AgentSnapshot(string agentId, string hostName, string tenantId = "")
    {
        AgentId = agentId;
        HostName = hostName;
        TenantId = tenantId;
    }

    public void AddMetric(MetricEvent e)
    {
        LastSeen = DateTimeOffset.UtcNow;

        foreach (var (key, value) in e.Values)
        {
            if (!_series.TryGetValue(key, out var q))
            {
                q = new Queue<(DateTimeOffset, double)>();
                _series[key] = q;
            }
            q.Enqueue((e.Timestamp, value));
            while (q.Count > MaxSeriesPoints) q.Dequeue();
        }
    }

    public void AddAnomaly(AnomalyResult a)
    {
        _anomalies.Enqueue(a);
        while (_anomalies.Count > 50) _anomalies.Dequeue();
    }

    public IReadOnlyList<AnomalyResult> RecentAnomalies => _anomalies.ToList();

    public int RecentMetricCount => _series.Values.Sum(q => q.Count);

    public List<TimeSeriesPoint> GetTimeSeries(string metricKey, DateTimeOffset since)
    {
        if (!_series.TryGetValue(metricKey, out var q))
            return new();

        return q
            .Where(p => p.ts >= since)
            .OrderBy(p => p.ts)
            .Select(p => new TimeSeriesPoint { Timestamp = p.ts, Value = p.val })
            .ToList();
    }

    public List<string> GetAvailableMetrics()
        => _series.Keys.ToList();

    public double HealthScore
    {
        get
        {
            double score = 100;

            if (_series.TryGetValue("cpu_percent", out var cpu) && cpu.Any())
            {
                var avg = cpu.Average(p => p.val);
                if (avg > 90) score -= 15;
                else if (avg > 75) score -= 5;
            }

            if (_series.TryGetValue("memory_percent", out var mem) && mem.Any())
            {
                var avg = mem.Average(p => p.val);
                if (avg > 90) score -= 10;
                else if (avg > 80) score -= 4;
            }

            if (_series.TryGetValue("error_rate", out var err) && err.Any())
                score -= err.Average(p => p.val) * 30;

            var recent1h = _anomalies
                .Where(a => a.DetectedAt >= DateTimeOffset.UtcNow.AddHours(-1))
                .ToList();
            score -= recent1h.Count(a => a.Severity == AnomalySeverity.Critical) * 20;
            score -= recent1h.Count(a => a.Severity == AnomalySeverity.High) * 10;
            score -= recent1h.Count(a => a.Severity == AnomalySeverity.Medium) * 5;

            return Math.Max(0, Math.Min(100, score));
        }
    }
}

public class TimeSeriesPoint
{
    public DateTimeOffset Timestamp { get; set; }
    public double Value { get; set; }
}

public class ActivityEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Level { get; set; } = "info";
}

public class DashboardSnapshot
{
    public List<AgentSnapshot> Agents { get; set; } = new();
    public List<AnomalyResult> RecentAnomalies { get; set; } = new();
    public int TotalAgents { get; set; }
    public int ActiveAgents { get; set; }
    public int AnomaliesLast1h { get; set; }
    public int CriticalAlerts { get; set; }
    public double AvgHealthScore { get; set; }
    public int IngestionRate { get; set; }
}