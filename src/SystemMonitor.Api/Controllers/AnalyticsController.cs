using Microsoft.AspNetCore.Mvc;
using SystemMonitor.Api.Services;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Controllers;

[ApiController]
[Route("api/v1/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analytics;

    public AnalyticsController(IAnalyticsService analytics)
        => _analytics = analytics;

    [HttpGet("anomalies")]
    public async Task<IActionResult> GetAnomalies(
        [FromQuery] string? agentId,
        [FromQuery] AnomalySeverity? minSeverity,
        [FromQuery] int limit = 50,
        [FromQuery] int offsetMinutes = 60,
        CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddMinutes(-offsetMinutes);
        var result = await _analytics.GetAnomaliesAsync(
            agentId, minSeverity, since, limit, ct);
        return Ok(result);
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealthScores(CancellationToken ct)
        => Ok(await _analytics.GetHealthScoresAsync(ct));

    [HttpGet("metrics/{agentId}/{metricKey}")]
    public async Task<IActionResult> GetMetricTimeSeries(
        string agentId,
        string metricKey,
        [FromQuery] int windowMinutes = 30,
        CancellationToken ct = default)
    {
        var since = DateTimeOffset.UtcNow.AddMinutes(-windowMinutes);
        var series = await _analytics.GetMetricTimeSeriesAsync(
            agentId, metricKey, since, windowMinutes, ct);
        return Ok(series);
    }

    [HttpGet("infrastructure")]
    public async Task<IActionResult> GetInfrastructureStatus(CancellationToken ct)
        => Ok(await _analytics.GetInfrastructureStatusAsync(ct));

    [HttpGet("summary")]
    public async Task<IActionResult> GetDashboardSummary(CancellationToken ct)
        => Ok(await _analytics.GetDashboardSummaryAsync(ct));

    [HttpGet("logs/recent")]
    public async Task<IActionResult> GetRecentLogs(
        [FromQuery] int limit = 200,
        CancellationToken ct = default)
        => Ok(await _analytics.GetRecentLogsAsync(limit, ct));

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivityLog(
        [FromQuery] int limit = 100,
        CancellationToken ct = default)
        => Ok(await _analytics.GetActivityLogAsync(limit, ct));

    [HttpGet("agents")]
    public IActionResult GetAgents()
        => Ok(_analytics.GetAvailableAgents());

    [HttpGet("metrics/{agentId}")]
    public IActionResult GetAvailableMetrics(string agentId)
        => Ok(_analytics.GetAvailableMetrics(agentId));
}