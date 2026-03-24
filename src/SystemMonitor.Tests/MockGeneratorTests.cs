using FluentAssertions;
using Microsoft.Extensions.Configuration;
using SystemMonitor.Agent.Generators;
using SystemMonitor.Api;
using SystemMonitor.Shared.Models;
using Xunit;

namespace SystemMonitor.Tests;

public class MockGeneratorTests
{
    private readonly IConfiguration _cfg;

    public MockGeneratorTests()
    {
        _cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:Id"] = "test-agent",
                ["Agent:HostName"] = "test-host",
                ["Agent:Environment"] = "test",
                ["MockData:AnomalyProbability"] = "0.0",
                ["MockData:LogBurstProbability"] = "0.0"
            })
            .Build();
    }

    [Fact]
    public void MockMetricGenerator_ShouldProduceValidMetric()
    {
        var gen = new MockMetricGenerator(_cfg);
        var metric = gen.Generate();

        metric.AgentId.Should().Be("test-agent");
        metric.HostName.Should().Be("test-host");
        metric.Values.Should().ContainKey("cpu_percent");
        metric.Values.Should().ContainKey("memory_percent");
        metric.Values.Should().ContainKey("error_rate");
        metric.Values.Should().ContainKey("p99_latency_ms");
    }

    [Fact]
    public void MockMetricGenerator_AllValuesShouldBeInPhysicalBounds()
    {
        var gen = new MockMetricGenerator(_cfg);

        for (int i = 0; i < 100; i++)
        {
            var m = gen.Generate();
            m.Values["cpu_percent"].Should().BeInRange(0, 100);
            m.Values["memory_percent"].Should().BeInRange(0, 100);
            m.Values["disk_percent"].Should().BeInRange(0, 100);
            m.Values["error_rate"].Should().BeInRange(0, 1);
            m.Values["p99_latency_ms"].Should().BeGreaterThanOrEqualTo(0);
            m.Values["requests_per_second"].Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public void MockMetricGenerator_WithAnomalyEnabled_ShouldEventuallyProduceAnomaly()
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Agent:Id"] = "test-agent",
                ["Agent:HostName"] = "test-host",
                ["MockData:AnomalyProbability"] = "1.0
            })
            .Build();

        var gen = new MockMetricGenerator(cfg);
        var metric = gen.Generate();

        metric.Tags["is_anomaly"].Should().Be("true");
    }

    [Fact]
    public void MockLogGenerator_ShouldProduceLogsWithAllFields()
    {
        var gen = new MockLogGenerator(_cfg);
        var batch = gen.GenerateBatch();

        batch.Should().NotBeEmpty();
        batch.All(l => !string.IsNullOrEmpty(l.Message)).Should().BeTrue();
        batch.All(l => !string.IsNullOrEmpty(l.ServiceName)).Should().BeTrue();
        batch.All(l => !string.IsNullOrEmpty(l.AgentId)).Should().BeTrue();
    }
}

public class CsvParserTests
{
    [Fact]
    public void ParseTrainingData_ValidCsv_ShouldReturnRecords()
    {
        const string csv = """
            timestamp,cpu_percent,memory_percent,disk_read_mbps,disk_write_mbps,network_in_mbps,network_out_mbps,requests_per_second,error_rate,p99_latency_ms,is_anomaly
            2024-01-15T10:00:00Z,45.2,67.8,12.5,8.3,23.1,4.7,142.0,0.02,245.0,0
            2024-01-15T10:00:10Z,92.1,88.4,15.1,9.2,24.0,5.1,139.0,0.35,1200.0,1
            """;

        var records = CsvParser.ParseTrainingData(csv);

        records.Should().HaveCount(2);
        records[0].CpuPercent.Should().BeApproximately(45.2, 0.01);
        records[0].IsAnomaly.Should().BeFalse();
        records[1].CpuPercent.Should().BeApproximately(92.1, 0.01);
        records[1].IsAnomaly.Should().BeTrue();
        records[1].ErrorRate.Should().BeApproximately(0.35, 0.001);
    }

    [Fact]
    public void ParseTrainingData_MalformedRows_ShouldSkipGracefully()
    {
        const string csv = """
            timestamp,cpu_percent,memory_percent,disk_read_mbps,disk_write_mbps,network_in_mbps,network_out_mbps,requests_per_second,error_rate,p99_latency_ms,is_anomaly
            2024-01-15T10:00:00Z,45.2,67.8,12.5,8.3,23.1,4.7,142.0,0.02,245.0,0
            THIS IS MALFORMED ROW
            2024-01-15T10:00:20Z,55.0,70.0,13.0,9.0,25.0,5.0,140.0,0.01,250.0,0
            """;

        var records = CsvParser.ParseTrainingData(csv);
        records.Should().HaveCount(2, because: "malformed row should be skipped, not crash");
    }

    [Fact]
    public void ParseTrainingData_EmptyCsv_ShouldReturnEmpty()
    {
        CsvParser.ParseTrainingData("").Should().BeEmpty();
        CsvParser.ParseTrainingData("just a header\n").Should().BeEmpty();
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void ParseTrainingData_BooleanLabel_ShouldParseAllVariants(string labelValue, bool expected)
    {
        string csv = $"timestamp,cpu_percent,memory_percent,disk_read_mbps,disk_write_mbps,network_in_mbps,network_out_mbps,requests_per_second,error_rate,p99_latency_ms,is_anomaly\n" +
                     $"2024-01-15T10:00:00Z,50,60,10,8,20,5,100,0.01,200,{labelValue}";

        var records = CsvParser.ParseTrainingData(csv);
        records.Should().HaveCount(1);
        records[0].IsAnomaly.Should().Be(expected);
    }
}
