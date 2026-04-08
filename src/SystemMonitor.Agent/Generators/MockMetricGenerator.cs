using SystemMonitor.Shared.Models;

namespace SystemMonitor.Agent.Generators;

public class MockMetricGenerator
{
    private readonly Random _rng = new();

    private int _tick;
    private bool _inAnomaly;
    private int _anomalyTicks;
    private string _anomalyMetric = "cpu_percent";

    private double _diskRead, _diskWrite, _netIn, _netOut;
    private double _rps, _errorRate, _p99;
    private double _connections, _queryTime, _cacheHits, _sessionCount;
    private double _firewallBlocked, _firewallAllowed, _intrusionAttempts;
    private double _gcPressure, _threadPool, _handleCount;

    public MetricEvent Generate(string agentId, string hostName, string profile, double anomalyProb)
    {
        _tick++;

        if (!_inAnomaly && _rng.NextDouble() < anomalyProb)
        {
            _inAnomaly = true;
            _anomalyTicks = _rng.Next(4, 18);
            _anomalyMetric = PickTarget(profile);
        }
        if (_inAnomaly && --_anomalyTicks <= 0) _inAnomaly = false;

        double phase = Math.Sin(2 * Math.PI * _tick / 8640.0);
        double phase2 = Math.Sin(2 * Math.PI * _tick / 2160.0);
        double noiseLow = Jitter(3);

        return profile switch
        {
            "firewall" => GenerateFirewall(agentId, hostName, phase, phase2),
            "database" => GenerateDatabase(agentId, hostName, phase, phase2),
            "cache" => GenerateCache(agentId, hostName, phase, phase2),
            "legacy" => GenerateLegacy(agentId, hostName, phase),
            _ => GenerateWeb(agentId, hostName, phase, phase2, noiseLow),
        };
    }

    private MetricEvent GenerateWeb(string agentId, string hostName, double phase, double phase2, double noise)
    {
        double cpuBase = 28 + 22 * phase + 8 * phase2 + Jitter(5);
        double memBase = 52 + 18 * phase + Jitter(3);

        double cpuBoost = Boost("cpu_percent", 40, 65);
        double memBoost = Boost("memory_percent", 22, 38);
        double errBoost = Boost("error_rate", 0.15, 0.45);
        double latBoost = Boost("p99_latency_ms", 600, 2500);

        _rps = Smooth(_rps, 180 + 120 * phase + 40 * phase2 + Jitter(25), 0.25);
        _errorRate = Smooth(_errorRate, 0.008 + errBoost + Jitter(0.002), 0.12);
        _p99 = Smooth(_p99, 65 + Jitter(20) + latBoost, 0.2);
        _netIn = Smooth(_netIn, 55 + 35 * phase + Jitter(12), 0.2);
        _netOut = Smooth(_netOut, 20 + 12 * phase + Jitter(6), 0.2);
        _diskRead = Smooth(_diskRead, 12 + Jitter(4), 0.3);
        _diskWrite = Smooth(_diskWrite, 6 + Jitter(3), 0.3);
        _sessionCount = Smooth(_sessionCount, 240 + 160 * phase + Jitter(30), 0.15);

        return Build(agentId, hostName, "web", new()
        {
            ["cpu_percent"] = Clamp(cpuBase + cpuBoost, 0, 100),
            ["memory_percent"] = Clamp(memBase + memBoost, 0, 100),
            ["disk_read_mbps"] = Clamp(_diskRead, 0, 500),
            ["disk_write_mbps"] = Clamp(_diskWrite, 0, 500),
            ["network_in_mbps"] = Clamp(_netIn, 0, 1000),
            ["network_out_mbps"] = Clamp(_netOut, 0, 1000),
            ["requests_per_second"] = Clamp(_rps, 0, 5000),
            ["error_rate"] = Clamp(_errorRate, 0, 1),
            ["p99_latency_ms"] = Clamp(_p99, 0, 30000),
            ["active_sessions"] = Clamp(_sessionCount, 0, 10000),
            ["disk_percent"] = Clamp(44 + Jitter(2), 0, 100),
            ["process_threads"] = Clamp(52 + Jitter(12), 1, 2000),
        });
    }

    private MetricEvent GenerateFirewall(string agentId, string hostName, double phase, double phase2)
    {
        double trafficBase = 800 + 500 * phase + 150 * phase2 + Jitter(80);
        double blockedBase = 45 + 30 * phase + Jitter(15);

        _firewallAllowed = Smooth(_firewallAllowed, trafficBase, 0.2);
        _firewallBlocked = Smooth(_firewallBlocked, blockedBase + Boost("blocked_sessions", 200, 800), 0.25);
        _intrusionAttempts = Smooth(_intrusionAttempts, 2 + Jitter(1) + Boost("intrusion_attempts", 30, 120), 0.18);
        _cpuRef = Smooth(_cpuRef, 18 + 12 * phase + Jitter(4) + Boost("cpu_percent", 35, 55), 0.2);
        _memRef = Smooth(_memRef, 38 + 8 * phase + Jitter(3), 0.2);
        _netIn = Smooth(_netIn, trafficBase * 0.08 + Jitter(20), 0.2);
        _netOut = Smooth(_netOut, trafficBase * 0.03 + Jitter(8), 0.2);

        return Build(agentId, hostName, "firewall", new()
        {
            ["cpu_percent"] = Clamp(_cpuRef, 0, 100),
            ["memory_percent"] = Clamp(_memRef, 0, 100),
            ["network_in_mbps"] = Clamp(_netIn, 0, 2000),
            ["network_out_mbps"] = Clamp(_netOut, 0, 2000),
            ["sessions_allowed"] = Clamp(_firewallAllowed, 0, 50000),
            ["sessions_blocked"] = Clamp(_firewallBlocked, 0, 10000),
            ["intrusion_attempts"] = Clamp(_intrusionAttempts, 0, 5000),
            ["policy_hits"] = Clamp(trafficBase * 0.95, 0, 50000),
            ["ssl_inspections"] = Clamp(trafficBase * 0.4, 0, 20000),
            ["active_tunnels"] = Clamp(12 + Jitter(4), 0, 500),
        });
    }

    private double _cpuRef, _memRef;

    private MetricEvent GenerateDatabase(string agentId, string hostName, double phase, double phase2)
    {
        double qpsBase = 420 + 280 * phase + 80 * phase2 + Jitter(40);

        _cpuRef = Smooth(_cpuRef, 22 + 28 * phase + Jitter(6) + Boost("cpu_percent", 45, 70), 0.2);
        _memRef = Smooth(_memRef, 68 + 14 * phase + Jitter(4) + Boost("memory_percent", 20, 28), 0.15);
        _connections = Smooth(_connections, 85 + 60 * phase + Jitter(15) + Boost("active_connections", 200, 450), 0.2);
        _queryTime = Smooth(_queryTime, 12 + Jitter(8) + Boost("query_time_ms", 200, 1200), 0.2);
        _diskRead = Smooth(_diskRead, 80 + 50 * phase + Jitter(20), 0.25);
        _diskWrite = Smooth(_diskWrite, 35 + 25 * phase + Jitter(12) + Boost("disk_write_mbps", 150, 400), 0.25);

        return Build(agentId, hostName, "database", new()
        {
            ["cpu_percent"] = Clamp(_cpuRef, 0, 100),
            ["memory_percent"] = Clamp(_memRef, 0, 100),
            ["disk_read_mbps"] = Clamp(_diskRead, 0, 1000),
            ["disk_write_mbps"] = Clamp(_diskWrite, 0, 1000),
            ["disk_percent"] = Clamp(62 + Jitter(3) + Boost("disk_percent", 20, 35), 0, 100),
            ["active_connections"] = Clamp(_connections, 0, 2000),
            ["queries_per_second"] = Clamp(qpsBase, 0, 10000),
            ["query_time_ms"] = Clamp(_queryTime, 0, 10000),
            ["replication_lag_ms"] = Clamp(Jitter(8) + 2 + Boost("replication_lag_ms", 800, 5000), 0, 30000),
            ["table_locks"] = Clamp(Jitter(3) + Boost("table_locks", 50, 200), 0, 1000),
            ["buffer_pool_hit_pct"] = Clamp(96 - Boost("buffer_miss_pct", 20, 40) + Jitter(1), 0, 100),
            ["deadlocks"] = Clamp(Boost("deadlocks", 5, 25) + Jitter(0.5), 0, 100),
        });
    }

    private MetricEvent GenerateCache(string agentId, string hostName, double phase, double phase2)
    {
        double reqBase = 2400 + 1800 * phase + 600 * phase2 + Jitter(200);

        _cacheHits = Smooth(_cacheHits, 94 - Boost("cache_hit_rate", 20, 40) + Jitter(1), 0.15);
        _cpuRef = Smooth(_cpuRef, 12 + 8 * phase + Jitter(3) + Boost("cpu_percent", 30, 55), 0.2);
        _memRef = Smooth(_memRef, 72 + 12 * phase + Jitter(4) + Boost("memory_percent", 18, 25), 0.15);
        _netIn = Smooth(_netIn, reqBase * 0.002 + Jitter(1), 0.2);
        _netOut = Smooth(_netOut, reqBase * 0.004 + Jitter(2), 0.2);

        return Build(agentId, hostName, "cache", new()
        {
            ["cpu_percent"] = Clamp(_cpuRef, 0, 100),
            ["memory_percent"] = Clamp(_memRef, 0, 100),
            ["network_in_mbps"] = Clamp(_netIn, 0, 500),
            ["network_out_mbps"] = Clamp(_netOut, 0, 500),
            ["requests_per_second"] = Clamp(reqBase, 0, 50000),
            ["cache_hit_rate"] = Clamp(_cacheHits, 0, 100),
            ["evictions_per_sec"] = Clamp(Jitter(5) + Boost("evictions_per_sec", 200, 800), 0, 5000),
            ["connected_clients"] = Clamp(38 + 22 * phase + Jitter(8), 0, 1000),
            ["used_memory_pct"] = Clamp(_memRef - 5 + Jitter(2), 0, 100),
            ["keyspace_hits"] = Clamp(reqBase * (_cacheHits / 100), 0, 50000),
            ["keyspace_misses"] = Clamp(reqBase * (1 - _cacheHits / 100), 0, 5000),
        });
    }

    private MetricEvent GenerateLegacy(string agentId, string hostName, double phase)
    {
        _gcPressure = Smooth(_gcPressure, Jitter(10) + 5 + Boost("gc_pressure", 40, 80), 0.2);
        _threadPool = Smooth(_threadPool, 48 + 30 * phase + Jitter(10) + Boost("thread_pool_queue", 200, 500), 0.2);
        _handleCount = Smooth(_handleCount, 1200 + 800 * phase + Jitter(100) + Boost("handle_count", 3000, 8000), 0.15);
        _cpuRef = Smooth(_cpuRef, 45 + 35 * phase + Jitter(12) + Boost("cpu_percent", 40, 50), 0.2);
        _memRef = Smooth(_memRef, 74 + 16 * phase + Jitter(6) + Boost("memory_percent", 18, 22), 0.15);
        _diskWrite = Smooth(_diskWrite, 18 + Jitter(8) + Boost("disk_write_mbps", 100, 250), 0.25);
        _errorRate = Smooth(_errorRate, 0.05 + Boost("error_rate", 0.2, 0.5) + Jitter(0.01), 0.12);
        _p99 = Smooth(_p99, 280 + Jitter(80) + Boost("p99_latency_ms", 1500, 5000), 0.2);

        return Build(agentId, hostName, "legacy", new()
        {
            ["cpu_percent"] = Clamp(_cpuRef, 0, 100),
            ["memory_percent"] = Clamp(_memRef, 0, 100),
            ["disk_write_mbps"] = Clamp(_diskWrite, 0, 1000),
            ["disk_percent"] = Clamp(78 + Jitter(4) + Boost("disk_percent", 15, 20), 0, 100),
            ["error_rate"] = Clamp(_errorRate, 0, 1),
            ["p99_latency_ms"] = Clamp(_p99, 0, 30000),
            ["process_threads"] = Clamp(_threadPool, 1, 2000),
            ["handle_count"] = Clamp(_handleCount, 0, 100000),
            ["gc_pause_ms"] = Clamp(_gcPressure * 15 + Jitter(5), 0, 5000),
            ["event_log_errors"] = Clamp(Jitter(3) + Boost("event_log_errors", 50, 200), 0, 1000),
            ["page_faults_per_sec"] = Clamp(120 + 80 * phase + Jitter(30) + Boost("page_faults_per_sec", 2000, 8000), 0, 50000),
        });
    }

    private MetricEvent Build(string agentId, string hostName, string profile,
        Dictionary<string, double> values)
        => new()
        {
            AgentId = agentId,
            HostName = hostName,
            Environment = "production",
            Type = MetricType.System,
            Values = values,
            Tags = new()
            {
                ["profile"] = profile,
                ["is_anomaly"] = _inAnomaly ? "true" : "false",
                ["mock"] = "true"
            }
        };

    private string PickTarget(string profile) => profile switch
    {
        "firewall" => Pick("cpu_percent", "sessions_blocked", "intrusion_attempts", "network_in_mbps"),
        "database" => Pick("cpu_percent", "memory_percent", "active_connections", "query_time_ms", "disk_write_mbps", "replication_lag_ms", "deadlocks"),
        "cache" => Pick("cpu_percent", "memory_percent", "cache_hit_rate", "evictions_per_sec"),
        "legacy" => Pick("cpu_percent", "memory_percent", "error_rate", "p99_latency_ms", "handle_count", "page_faults_per_sec"),
        _ => Pick("cpu_percent", "memory_percent", "error_rate", "p99_latency_ms", "network_in_mbps"),
    };

    private string Pick(params string[] options) => options[_rng.Next(options.Length)];

    private double Boost(string metric, double low, double high)
        => _inAnomaly && _anomalyMetric == metric ? low + _rng.NextDouble() * (high - low) : 0;

    private double Jitter(double scale) => (_rng.NextDouble() - 0.5) * 2 * scale;

    private static double Smooth(double cur, double target, double alpha)
        => cur == 0 ? target : cur + alpha * (target - cur);

    private static double Clamp(double v, double min, double max)
        => Math.Max(min, Math.Min(max, v));
}