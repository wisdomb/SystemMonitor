using Microsoft.Extensions.Logging;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Services;

public interface IAnalyticsService
{
    Task<IReadOnlyList<AnomalyResult>> GetAnomaliesAsync(
        string? agentId, AnomalySeverity? minSeverity,
        DateTimeOffset since, int limit, CancellationToken ct);
    Task<Dictionary<string, double>> GetHealthScoresAsync(CancellationToken ct);
    Task<IReadOnlyList<object>> GetMetricTimeSeriesAsync(
        string agentId, string metricKey, DateTimeOffset since,
        int windowMinutes, CancellationToken ct);
    Task<object> GetInfrastructureStatusAsync(CancellationToken ct);
    Task<object> GetDashboardSummaryAsync(CancellationToken ct);
    Task<IReadOnlyList<object>> GetRecentLogsAsync(int limit, CancellationToken ct);
    Task<IReadOnlyList<object>> GetActivityLogAsync(int limit, CancellationToken ct);
    List<string> GetAvailableAgents();
    List<string> GetAvailableMetrics(string agentId);
}

public class AnalyticsService : IAnalyticsService
{
    private readonly TelemetryCache _cache;
    private readonly ILogger<AnalyticsService> _logger;

    public AnalyticsService(
        TelemetryCache cache,
        ILogger<AnalyticsService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public Task<IReadOnlyList<AnomalyResult>> GetAnomaliesAsync(
        string? agentId, AnomalySeverity? minSeverity,
        DateTimeOffset since, int limit, CancellationToken ct)
    {
        var snapshot = _cache.GetSnapshot();
        var results = snapshot.RecentAnomalies
            .Where(a => a.DetectedAt >= since)
            .Where(a => agentId == null || a.AgentId == agentId)
            .Where(a => minSeverity == null || a.Severity >= minSeverity)
            .OrderByDescending(a => a.DetectedAt)
            .Take(limit)
            .ToList();

        return Task.FromResult<IReadOnlyList<AnomalyResult>>(results);
    }

    public Task<Dictionary<string, double>> GetHealthScoresAsync(CancellationToken ct)
        => Task.FromResult(_cache.GetHealthScores());

    public Task<IReadOnlyList<object>> GetMetricTimeSeriesAsync(
        string agentId, string metricKey, DateTimeOffset since,
        int windowMinutes, CancellationToken ct)
    {
        var points = _cache.GetTimeSeries(agentId, metricKey, windowMinutes);
        var result = points
            .Select(p => (object)new { timestamp = p.Timestamp, value = p.Value })
            .ToList();

        return Task.FromResult<IReadOnlyList<object>>(result);
    }

    public Task<object> GetInfrastructureStatusAsync(CancellationToken ct)
    {
        var snapshot = _cache.GetSnapshot();
        return Task.FromResult<object>(new
        {
            metricQueueDepth = 0,
            logQueueDepth = 0,
            workerCount = Math.Max(1, snapshot.ActiveAgents),
            processingDelayMs = 0,
            cosmosRequestUnits = 0.0
        });
    }

    public Task<object> GetDashboardSummaryAsync(CancellationToken ct)
    {
        var snapshot = _cache.GetSnapshot();
        return Task.FromResult<object>(new
        {
            totalAgents = snapshot.TotalAgents,
            activeAgents = snapshot.ActiveAgents,
            anomaliesLast1h = snapshot.AnomaliesLast1h,
            criticalAlerts = snapshot.CriticalAlerts,
            ingestionRatePerMin = snapshot.IngestionRate,
            avgHealthScore = Math.Round(snapshot.AvgHealthScore, 1),
            errorRatePercent = 0.0,
            avgLatencyMs = 0.0
        });
    }

    public Task<IReadOnlyList<object>> GetRecentLogsAsync(int limit, CancellationToken ct)
    {
        var logs = _cache.GetRecentLogs(limit);
        var result = logs.Select(l => (object)new
        {
            id = l.Id,
            agentId = l.AgentId,
            hostName = l.HostName,
            serviceName = l.ServiceName,
            level = l.Level.ToString(),
            message = l.Message,
            timestamp = l.Timestamp,
            stackTrace = l.StackTrace,
            properties = l.Properties
        }).ToList();

        return Task.FromResult<IReadOnlyList<object>>(result);
    }

    public Task<IReadOnlyList<object>> GetActivityLogAsync(int limit, CancellationToken ct)
    {
        var activity = _cache.GetActivityLog(limit);
        var result = activity.Select(a => (object)new
        {
            timestamp = a.Timestamp,
            message = a.Message,
            level = a.Level
        }).ToList();

        return Task.FromResult<IReadOnlyList<object>>(result);
    }

    public List<string> GetAvailableAgents()
        => _cache.GetActiveAgentIds();

    public List<string> GetAvailableMetrics(string agentId)
    {
        var snapshot = _cache.GetSnapshot();
        var agent = snapshot.Agents.FirstOrDefault(a => a.AgentId == agentId);
        return agent?.GetAvailableMetrics() ?? new List<string>();
    }
}