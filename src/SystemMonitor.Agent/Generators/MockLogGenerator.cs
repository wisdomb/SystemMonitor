using SystemMonitor.Shared.Models;
using SharedLogLevel = SystemMonitor.Shared.Models.LogLevel;

namespace SystemMonitor.Agent.Generators;

public class MockLogGenerator
{
    private readonly Random _rng = new();
    private bool _inBurst;
    private int _burstRemaining;

    private static readonly Dictionary<string, string[]> Services = new()
    {
        ["web"] = ["api-gateway", "auth-svc", "payments-svc", "notification-svc", "session-mgr"],
        ["firewall"] = ["policy-engine", "threat-intel", "ssl-inspector", "vpn-daemon", "log-collector"],
        ["database"] = ["query-executor", "replication-mgr", "vacuum-worker", "checkpoint-writer", "stats-collector"],
        ["cache"] = ["keyspace-mgr", "eviction-policy", "persistence-writer", "cluster-sync", "sentinel"],
        ["legacy"] = ["app-pool-1", "app-pool-2", "com-interop", "scheduled-tasks", "event-logger"],
    };

    private static readonly Dictionary<string, string[]> InfoMessages = new()
    {
        ["web"] =
        [
            "GET /api/v1/accounts/{0} — 200 OK in {1}ms",
            "POST /api/v1/payments — 201 Created in {1}ms",
            "Session refreshed for userId={2}",
            "Cache hit ratio: {3:.1f}%",
            "Rate limiter: {4}/1000 requests used for client {5}",
            "OAuth token issued for client_id={5}",
            "Webhook delivered to {6} in {1}ms",
            "Background sync completed: {7} records processed",
        ],
        ["firewall"] =
        [
            "Policy rule 'ALLOW_OUTBOUND_HTTPS' matched — {4} packets",
            "VPN tunnel {0} established from {6}",
            "SSL inspection passed for {6}:{4}",
            "IPS signature DB updated to version {7}",
            "Traffic shaping applied to zone DMZ — {4} Mbps",
            "FortiGuard lookup resolved for {6}",
        ],
        ["database"] =
        [
            "VACUUM completed on table orders — {7} dead tuples removed",
            "Checkpoint completed in {1}ms — {7} pages written",
            "Replication lag: {1}ms on standby replica",
            "Index scan on accounts.idx_created_at — {1}ms",
            "Connection {0} acquired from pool (pool size: {4})",
            "Auto-analyze triggered on high-churn table transactions",
        ],
        ["cache"] =
        [
            "KEYSPACE_HIT for user:{2}:session — TTL {1}s remaining",
            "SET user:{2}:prefs — {7} bytes written",
            "AOF rewrite completed — {7} bytes",
            "Cluster slot {4} migrated to node {0}",
            "Eviction policy maxmemory-policy=allkeys-lru active",
            "Sentinel promoted replica {0} to primary",
        ],
        ["legacy"] =
        [
            "Application pool '{5}' recycled — uptime was {7} hours",
            "COM+ component '{5}' activated successfully",
            "Scheduled task 'DailyReport' completed in {1}ms",
            "IIS worker process {0} started — PID {2}",
            "ASP session created for user {2} — timeout 20min",
        ],
    };

    private static readonly Dictionary<string, string[]> WarnMessages = new()
    {
        ["web"] =
        [
            "Response time elevated: {1}ms — SLA threshold 500ms",
            "Downstream payments-api returning 429 Too Many Requests",
            "Memory pressure detected — GC collecting Gen2",
            "Connection pool at {3:.0f}% capacity — consider scaling",
            "JWT expiry warning — token expires in {1}s for userId={2}",
        ],
        ["firewall"] =
        [
            "Unusual outbound traffic from zone INTERNAL to {6} — {4} packets",
            "IPS detected suspicious pattern: Port scan from {6}",
            "SSL certificate expiring in {1} days for {6}",
            "Policy violation: DENY_TORRENT matched {4} times in last hour",
            "Threat score elevated for source {6} — score {3:.0f}/100",
        ],
        ["database"] =
        [
            "Long-running query detected: {1}ms — query plan attached",
            "Replication lag exceeded threshold: {1}ms",
            "Table bloat detected on orders — consider VACUUM FULL",
            "Connection pool exhaustion risk — {4}/{3} connections active",
            "Autovacuum blocked by long-running transaction — waiting {1}s",
        ],
        ["cache"] =
        [
            "Cache hit rate dropped to {3:.1f}% — below 85% threshold",
            "Memory usage at {3:.0f}% — approaching maxmemory limit",
            "Eviction rate elevated: {4} evictions/sec",
            "Cluster node {0} unreachable — failover may trigger",
            "AOF write delay {1}ms — disk I/O pressure",
        ],
        ["legacy"] =
        [
            "Application pool '{5}' reached 80% memory limit",
            "Thread pool queue depth: {4} pending requests",
            "COM+ transaction timeout approaching — {1}ms elapsed",
            "Disk fragmentation at {3:.0f}% — defrag recommended",
            "Event log approaching capacity — {4} entries remaining",
        ],
    };

    private static readonly Dictionary<string, string[]> ErrorMessages = new()
    {
        ["web"] =
        [
            "Unhandled exception: NullReferenceException in PaymentController.ProcessPayment",
            "Database connection timeout after 30s — circuit breaker OPEN",
            "HTTP 503 from upstream payments-api — all retries exhausted",
            "JWT signature validation failed — possible token tampering",
            "Redis connection refused: ECONNRESET — session data unavailable",
            "OutOfMemoryException in background worker — thread terminated",
            "Deadlock detected in order processing pipeline",
        ],
        ["firewall"] =
        [
            "IPS BLOCK: SQL injection attempt from {6} — rule FG-IPS-SQLi-001",
            "DDoS threshold exceeded: {4} pps from {6} — auto-block applied",
            "SSL handshake failure: certificate mismatch for {6}",
            "VPN tunnel {0} dropped — peer unreachable after {1}s",
            "FortiGuard category BOTNET matched for {6} — traffic blocked",
            "High severity threat detected: CVE-2024-{0} exploitation attempt",
        ],
        ["database"] =
        [
            "FATAL: max_connections ({4}) exceeded — new connection refused",
            "ERROR: deadlock detected between transactions {0} and {2}",
            "PANIC: WAL segment corruption detected — immediate attention required",
            "Replication slot {0} has fallen {7} GB behind — disk risk",
            "OOM killer terminated postgres worker — shared_buffers too large",
            "Constraint violation: foreign key on orders.user_id — data integrity issue",
        ],
        ["cache"] =
        [
            "CLUSTERDOWN: cluster cannot accept writes — majority lost",
            "OOM: maxmemory policy evicted {7} keys — data loss occurred",
            "Replication link broken between master and replica {0}",
            "AOF persistence failed — disk full on /var/lib/redis",
            "Sentinel quorum lost — automatic failover suspended",
        ],
        ["legacy"] =
        [
            "Application pool '{5}' crashed — worker process terminated",
            "Unhandled COM exception 0x80131500 in component '{5}'",
            "Stack overflow in recursive call — thread {0} terminated",
            "Registry key access denied — missing DCOM permissions",
            "IIS worker process recycled due to memory limit breach",
            "EventLog error: Source '{5}' write failed — buffer overflow",
        ],
    };

    public IReadOnlyList<LogEvent> Generate(
        string agentId, string hostName, string profile, double burstProb)
    {
        if (!_inBurst && _rng.NextDouble() < burstProb)
        {
            _inBurst = true;
            _burstRemaining = _rng.Next(20, 55);
        }

        int count = _inBurst ? _rng.Next(10, 20) : _rng.Next(2, 8);
        var batch = new List<LogEvent>(count);
        var svcList = Services.TryGetValue(profile, out var s) ? s : Services["web"];

        for (int i = 0; i < count; i++)
        {
            var (level, msg) = PickMessage(profile);
            batch.Add(new LogEvent
            {
                AgentId = agentId,
                HostName = hostName,
                ServiceName = svcList[_rng.Next(svcList.Length)],
                Environment = "production",
                Level = level,
                Message = Format(msg),
                StackTrace = level is SharedLogLevel.Error or SharedLogLevel.Critical
                    ? Stacktrace(profile)
                    : null,
                Properties = new()
                {
                    ["traceId"] = Guid.NewGuid().ToString("N")[..16],
                    ["profile"] = profile,
                    ["burst"] = _inBurst ? "true" : "false",
                }
            });
        }

        if (_inBurst)
        {
            _burstRemaining -= count;
            if (_burstRemaining <= 0) _inBurst = false;
        }

        return batch;
    }

    private (SharedLogLevel, string) PickMessage(string profile)
    {
        var infos = InfoMessages.TryGetValue(profile, out var i) ? i : InfoMessages["web"];
        var warns = WarnMessages.TryGetValue(profile, out var w) ? w : WarnMessages["web"];
        var errors = ErrorMessages.TryGetValue(profile, out var e) ? e : ErrorMessages["web"];

        if (_inBurst)
            return _rng.NextDouble() < 0.65
                ? (SharedLogLevel.Error, errors[_rng.Next(errors.Length)])
                : (SharedLogLevel.Critical, errors[_rng.Next(errors.Length)]);

        double r = _rng.NextDouble();
        return r switch
        {
            < 0.50 => (SharedLogLevel.Information, infos[_rng.Next(infos.Length)]),
            < 0.72 => (SharedLogLevel.Debug, infos[_rng.Next(infos.Length)]),
            < 0.88 => (SharedLogLevel.Warning, warns[_rng.Next(warns.Length)]),
            < 0.97 => (SharedLogLevel.Error, errors[_rng.Next(errors.Length)]),
            _ => (SharedLogLevel.Critical, errors[_rng.Next(errors.Length)]),
        };
    }

    private string Format(string t) => string.Format(t,
        _rng.Next(1000, 9999),
        _rng.Next(10, 5000),
        _rng.Next(10000, 99999),
        Math.Round(_rng.NextDouble() * 100, 1),
        _rng.Next(5, 500),
        new[] { "svc-auth", "svc-pay", "svc-notify", "svc-report" }[_rng.Next(4)],
        $"192.168.{_rng.Next(1, 10)}.{_rng.Next(1, 254)}",
        _rng.Next(100, 100000)
    );

    private string Stacktrace(string profile)
    {
        var frames = profile switch
        {
            "database" => new[]
            {
                "   at Npgsql.NpgsqlCommand.ExecuteReaderAsync()",
                "   at DatabaseProxy.QueryExecutor.RunAsync(String sql)",
                "   at DatabaseProxy.Controllers.QueryController.Execute()",
            },
            "firewall" => new[]
            {
                "   at FortiOS.PolicyEngine.Evaluate(Packet pkt)",
                "   at FortiOS.SessionTable.Lookup(SessionKey key)",
                "   at FortiOS.Daemon.ProcessPacket(RawFrame frame)",
            },
            "cache" => new[]
            {
                "   at StackExchange.Redis.ConnectionMultiplexer.ExecuteAsync()",
                "   at CacheSvc.KeyspaceMgr.SetAsync(String key, Byte[] val)",
                "   at CacheSvc.ClusterSync.Replicate(ReplicationFrame frame)",
            },
            _ => new[]
            {
                "   at SystemMonitor.Api.Controllers.IngestionController.IngestMetrics()",
                "   at Microsoft.AspNetCore.Mvc.Infrastructure.ActionMethodExecutor.Execute()",
                "   at Microsoft.AspNetCore.Routing.EndpointMiddleware.Invoke(HttpContext context)",
            }
        };
        return string.Join('\n', frames.Take(_rng.Next(2, frames.Length + 1)));
    }
}