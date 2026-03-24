using Microsoft.Extensions.Configuration;
using SystemMonitor.Shared.Models;
using SharedLogLevel = SystemMonitor.Shared.Models.LogLevel;

namespace SystemMonitor.Agent.Generators;

public class MockLogGenerator
{
    private readonly IConfiguration _cfg;
    private readonly Random _rng = new();

    private static readonly string[] Services =
        ["api-gateway", "payments-svc", "auth-svc", "database-proxy", "cache-svc", "worker"];

    private static readonly string[] InfoMessages =
    [
        "Request processed successfully in {0}ms",
        "Cache hit for key '{1}'",
        "Background job '{2}' completed ({3} records)",
        "Health check passed",
        "Connection pool size: {4}",
        "Scheduled task started: {2}",
        "User session refreshed for userId={5}",
        "Metrics flushed to upstream",
    ];

    private static readonly string[] WarnMessages =
    [
        "Response time elevated: {0}ms (threshold 500ms)",
        "Cache miss rate above threshold: {6:.1f}%",
        "Retry attempt {7}/3 for downstream call",
        "Database connection pool at 80% capacity",
        "Rate limit approaching for client {5}",
        "Disk usage at 78% — consider cleanup",
    ];

    private static readonly string[] ErrorMessages =
    [
        "Unhandled exception in request pipeline: NullReferenceException",
        "Database connection timeout after 30s — retrying",
        "HTTP 503 received from upstream payments-api",
        "Failed to acquire distributed lock after 10s",
        "JWT validation failed: token expired",
        "Redis connection refused: Connection reset by peer",
        "OutOfMemoryException in background worker thread",
        "SQL deadlock detected — transaction rolled back",
    ];

    private int _errorBurstRemaining;
    private bool _inBurst;

    public MockLogGenerator(IConfiguration cfg) => _cfg = cfg;

    public IReadOnlyList<LogEvent> Generate(string agentId, string hostName, double burstProb)
    {
        return GenerateBatch(agentId, hostName, burstProb);
    }

    public IReadOnlyList<LogEvent> GenerateBatch(string agentId, string hostName, double burstProb)
    {
        if (!_inBurst && _rng.NextDouble() < burstProb)
        {
            _inBurst = true;
            _errorBurstRemaining = _rng.Next(15, 40);
        }

        int count = _inBurst ? _rng.Next(8, 16) : _rng.Next(1, 6);
        var batch = new List<LogEvent>(count);

        for (int i = 0; i < count; i++)
        {
            var (level, message) = PickLevelAndMessage();
            batch.Add(new LogEvent
            {
                AgentId = agentId,
                HostName = hostName,
                ServiceName = Services[_rng.Next(Services.Length)],
                Environment = _cfg["Agent:Environment"] ?? "development",
                Level = level,
                Message = FormatMessage(message),
                StackTrace = level is SharedLogLevel.Error or SharedLogLevel.Critical
                    ? GenerateFakeStackTrace()
                    : null,
                Properties = new Dictionary<string, string>
                {
                    ["traceId"] = Guid.NewGuid().ToString("N")[..16],
                    ["mock"] = "true",
                    ["burst"] = _inBurst ? "true" : "false"
                }
            });
        }

        if (_inBurst)
        {
            _errorBurstRemaining -= count;
            if (_errorBurstRemaining <= 0) _inBurst = false;
        }

        return batch;
    }

    private (SharedLogLevel, string) PickLevelAndMessage()
    {
        if (_inBurst)
        {
            return _rng.NextDouble() < 0.7
                ? (SharedLogLevel.Error, ErrorMessages[_rng.Next(ErrorMessages.Length)])
                : (SharedLogLevel.Critical, ErrorMessages[_rng.Next(ErrorMessages.Length)]);
        }

        double r = _rng.NextDouble();
        return r switch
        {
            < 0.55 => (SharedLogLevel.Information, InfoMessages[_rng.Next(InfoMessages.Length)]),
            < 0.75 => (SharedLogLevel.Debug, InfoMessages[_rng.Next(InfoMessages.Length)]),
            < 0.90 => (SharedLogLevel.Warning, WarnMessages[_rng.Next(WarnMessages.Length)]),
            < 0.98 => (SharedLogLevel.Error, ErrorMessages[_rng.Next(ErrorMessages.Length)]),
            _ => (SharedLogLevel.Critical, ErrorMessages[_rng.Next(ErrorMessages.Length)])
        };
    }

    private string FormatMessage(string template)
    {
        return string.Format(template,
            _rng.Next(10, 2000),
            $"user:{_rng.Next(1000, 9999)}:profile",
            new[] { "cleanup", "report", "sync", "backup" }[_rng.Next(4)],
            _rng.Next(1, 50000),
            _rng.Next(5, 100),
            _rng.Next(10000, 99999),
            _rng.NextDouble() * 30,
            _rng.Next(1, 3)
        );
    }

    private string GenerateFakeStackTrace()
    {
        string[] frames =
        [
            "   at SystemMonitor.Api.Controllers.IngestionController.IngestMetrics()",
            "   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.Execute()",
            "   at Microsoft.AspNetCore.Routing.EndpointMiddleware.Invoke(HttpContext context)",
            "   at Serilog.AspNetCore.RequestLoggingMiddleware.Invoke(HttpContext httpContext)",
        ];
        return string.Join('\n', frames.Take(_rng.Next(2, frames.Length + 1)));
    }
}