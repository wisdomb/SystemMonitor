using SystemMonitor.Agent.Generators;
using SystemMonitor.Shared.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SharedLogLevel = SystemMonitor.Shared.Models.LogLevel;

namespace SystemMonitor.DataSeeder;


public class MockGeneratorAdapter
{
    private readonly MockMetricGenerator _inner;
    private readonly string _agentId;
    private readonly string _hostName;

    public MockGeneratorAdapter(string agentId, string hostName)
    {
        _agentId = agentId;
        _hostName = hostName;

        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:Id"] = agentId,
                ["Agent:HostName"] = hostName,
                ["Agent:Environment"] = "development",
                ["MockData:AnomalyProbability"] = "0.04",
                ["MockData:LogBurstProbability"] = "0.02"
            })
            .Build();

        _inner = new MockMetricGenerator(cfg);
    }

    public MetricEvent Generate(DateTimeOffset timestamp)
    {
        var evt = _inner.Generate();
        return evt with { Timestamp = timestamp };
    }
}

public class MockLogAdapter
{
    private readonly MockLogGenerator _inner;

    public MockLogAdapter(string agentId, string hostName)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:Id"] = agentId,
                ["Agent:HostName"] = hostName,
                ["Agent:Environment"] = "development",
                ["MockData:LogBurstProbability"] = "0.02"
            })
            .Build();

        _inner = new MockLogGenerator(cfg);
    }

    public IReadOnlyList<LogEvent> GenerateBatch(DateTimeOffset timestamp)
    {
        return _inner.GenerateBatch()
            .Select(e => e with { Timestamp = timestamp })
            .ToList();
    }
}

public static class TrainingDataGenerator
{
    private static readonly Random Rng = new();

    public static string GenerateCsv(int recordCount)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:Id"] = "training-gen",
                ["Agent:HostName"] = "generator",
                ["MockData:AnomalyProbability"] = "0.10"
            })
            .Build();

        var gen = new MockMetricGenerator(cfg);
        var rows = new List<string>(recordCount + 1);

        rows.Add("timestamp,cpu_percent,memory_percent,disk_read_mbps,disk_write_mbps," +
                "network_in_mbps,network_out_mbps,requests_per_second,error_rate," +
                "p99_latency_ms,is_anomaly");

        var baseTime = DateTimeOffset.UtcNow.AddDays(-7);

        for (int i = 0; i < recordCount; i++)
        {
            var evt = gen.Generate();
            var timestamp = baseTime.AddSeconds(i * 10);
            bool anomaly = evt.Tags.GetValueOrDefault("is_anomaly") == "true";

            rows.Add(string.Join(',', [
                timestamp.ToString("O"),
                F(evt.Values, "cpu_percent"),
                F(evt.Values, "memory_percent"),
                F(evt.Values, "disk_read_mbps"),
                F(evt.Values, "disk_write_mbps"),
                F(evt.Values, "network_in_mbps"),
                F(evt.Values, "network_out_mbps"),
                F(evt.Values, "requests_per_second"),
                F(evt.Values, "error_rate"),
                F(evt.Values, "p99_latency_ms"),
                anomaly ? "1" : "0"
            ]));
        }

        return string.Join('\n', rows);
    }

    private static string F(Dictionary<string, double> d, string key)
        => d.TryGetValue(key, out var v) ? v.ToString("F4") : "0";
}
