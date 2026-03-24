using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SystemMonitor.Api.Services;

namespace SystemMonitor.Api.Controllers;

[ApiController]
[Route("api/v1/health")]
public class HealthController : ControllerBase
{
    private readonly TelemetryCache _cache;
    private readonly IConfiguration _cfg;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        TelemetryCache cache,
        IConfiguration cfg,
        ILogger<HealthController> logger)
    {
        _cache = cache;
        _cfg = cfg;
        _logger = logger;
    }

    [HttpGet("live")]
    public IActionResult Liveness()
        => Ok(new { status = "alive", timestamp = DateTimeOffset.UtcNow });

    [HttpGet]
    [HttpGet("ready")]
    public IActionResult Readiness()
    {
        var snapshot = _cache.GetSnapshot();
        var useInMemory = _cfg.GetValue<bool>("ServiceBus:UseInMemory");

        var checks = new Dictionary<string, object>
        {
            ["api"] = new { healthy = true, message = "ok", latencyMs = 0 },
            ["ingestion_mode"] = new
            {
                healthy = true,
                message = useInMemory ? "in-memory (local dev)" : "azure-service-bus",
                latencyMs = 0
            },
            ["active_agents"] = new
            {
                healthy = snapshot.ActiveAgents >= 0,
                message = $"{snapshot.ActiveAgents} agent(s) reporting",
                latencyMs = 0
            }
        };

        return Ok(new
        {
            status = "healthy",
            timestamp = DateTimeOffset.UtcNow,
            version = GetType().Assembly.GetName().Version?.ToString() ?? "1.0.0",
            environment = _cfg["ASPNETCORE_ENVIRONMENT"] ?? "Production",
            mode = useInMemory ? "local-dev" : "azure",
            activeAgents = snapshot.ActiveAgents,
            checks
        });
    }
}