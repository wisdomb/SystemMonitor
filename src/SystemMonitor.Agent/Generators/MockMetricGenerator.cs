using Microsoft.Extensions.Configuration;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Agent.Generators;

public class MockMetricGenerator
{
    private readonly IConfiguration _cfg;
    private readonly Random _rng = new();

    private int _anomalyTicksRemaining;
    private bool _inAnomaly;
    private string _anomalyMetric = "cpu_percent";

    private double _diskReadMbps;
    private double _diskWriteMbps;
    private double _netInMbps;
    private double _netOutMbps;
    private double _rps;
    private double _errorRate;
    private double _p99Ms;

    private int _tick;

    public MockMetricGenerator(IConfiguration cfg) => _cfg = cfg;

    public MetricEvent Generate(string agentId, string hostName, double anomalyProb)
    {
        _tick++;
        if (!_inAnomaly && _rng.NextDouble() < anomalyProb)
        {
            _inAnomaly = true;
            _anomalyTicksRemaining = _rng.Next(3, 12);
            _anomalyMetric = PickAnomalyTarget();
        }

        if (_inAnomaly)
        {
            _anomalyTicksRemaining--;
            if (_anomalyTicksRemaining <= 0) _inAnomaly = false;
        }

        double dayPhase = Math.Sin(2 * Math.PI * _tick / 8640.0);
        double cpuBase = 35 + 25 * dayPhase + Jitter(8);
        double memBase = 55 + 15 * dayPhase + Jitter(4);

        double cpuBoost = _inAnomaly && _anomalyMetric == "cpu_percent" ? _rng.Next(35, 60) : 0;
        double memBoost = _inAnomaly && _anomalyMetric == "memory_percent" ? _rng.Next(25, 40) : 0;
        double errBoost = _inAnomaly && _anomalyMetric == "error_rate" ? _rng.NextDouble() * 0.4 : 0;
        double latBoost = _inAnomaly && _anomalyMetric == "p99_latency_ms" ? _rng.Next(500, 2000) : 0;
        double diskBoost = _inAnomaly && _anomalyMetric == "disk_write_mbps" ? _rng.Next(100, 300) : 0;

        _diskReadMbps = Smooth(_diskReadMbps, 15 + Jitter(5) + diskBoost / 3, 0.3);
        _diskWriteMbps = Smooth(_diskWriteMbps, 8 + Jitter(4) + diskBoost, 0.3);
        _netInMbps = Smooth(_netInMbps, 40 + Jitter(15) + (_inAnomaly && _anomalyMetric == "network_in_mbps" ? 200 : 0), 0.25);
        _netOutMbps = Smooth(_netOutMbps, 12 + Jitter(5), 0.25);
        _rps = Smooth(_rps, 120 + 80 * dayPhase + Jitter(20), 0.2);
        _errorRate = Smooth(_errorRate, 0.01 + errBoost, 0.15);
        _p99Ms = Smooth(_p99Ms, 80 + Jitter(30) + latBoost, 0.2);

        return new MetricEvent
        {
            AgentId = agentId,
            HostName = hostName,
            Environment = "production",
            Type = MetricType.System,
            Values = new Dictionary<string, double>
            {
                ["cpu_percent"] = Clamp(cpuBase + cpuBoost, 0, 100),
                ["memory_percent"] = Clamp(memBase + memBoost, 0, 100),
                ["disk_read_mbps"] = Clamp(_diskReadMbps, 0, 1000),
                ["disk_write_mbps"] = Clamp(_diskWriteMbps, 0, 1000),
                ["network_in_mbps"] = Clamp(_netInMbps, 0, 1000),
                ["network_out_mbps"] = Clamp(_netOutMbps, 0, 1000),
                ["disk_percent"] = Clamp(42 + Jitter(1), 0, 100),
                ["requests_per_second"] = Clamp(_rps, 0, 10000),
                ["error_rate"] = Clamp(_errorRate, 0, 1),
                ["p99_latency_ms"] = Clamp(_p99Ms, 0, 30000),
                ["process_threads"] = Clamp(48 + Jitter(10), 1, 10000),
                ["gc_gen0_collections"] = _tick * 3,
                ["gc_gen1_collections"] = _tick / 5,
                ["gc_gen2_collections"] = _tick / 50,
            },
            Tags = new Dictionary<string, string>
            {
                ["mode"] = "mock",
                ["is_anomaly"] = _inAnomaly ? "true" : "false",
                ["anomaly_type"] = _inAnomaly ? _anomalyMetric : "none"
            }
        };
    }

    private string PickAnomalyTarget()
    {
        string[] targets = [
            "cpu_percent", "memory_percent", "error_rate",
            "p99_latency_ms", "disk_write_mbps", "network_in_mbps"
        ];
        return targets[_rng.Next(targets.Length)];
    }

    private double Jitter(double scale) => (_rng.NextDouble() - 0.5) * 2 * scale;

    private static double Smooth(double current, double target, double alpha)
        => current == 0 ? target : current + alpha * (target - current);

    private static double Clamp(double v, double min, double max)
        => Math.Max(min, Math.Min(max, v));
}