using FluentAssertions;
using SystemMonitor.Api.Services;
using Xunit;

namespace SystemMonitor.Tests;

public class AttributeNormalizationServiceTests
{

    [Theory]
    [InlineData("cpu_percent", "cpupercent")]
    [InlineData("CPU Percent", "cpupercent")]
    [InlineData("cpuPercent", "cpupercent")]
    [InlineData("CPUPercent", "cpupercent")]
    [InlineData("cpu-percent", "cpupercent")]
    [InlineData("CPU %", "cpu")]
    [InlineData("Processor Usage", "processorusage")]
    [InlineData("processorUsage", "processorusage")]
    [InlineData("PROCESSOR_USAGE", "processorusage")]
    public void NormaliseKey_ShouldProduceConsistentKey(string input, string expected)
    {
        var result = AttributeNormalizationService.NormaliseKey(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("cpu_percent", "cpu_percent", 1.0)]
    [InlineData("name", "full name", 0.0)]
    [InlineData("cpu", "gpu", 0.0)]
    public void JaroWinkler_IdenticalStrings_ShouldReturnOne(
        string a, string b, double minExpected)
    {
        var score = AttributeNormalizationService.JaroWinkler(a, b);
        score.Should().BeGreaterThanOrEqualTo(minExpected);
    }

    [Theory]
    [InlineData("cpu_percent", "cpu_precentage", 0.85)]
    [InlineData("memory_usage", "memory_usege", 0.90)]
    [InlineData("diskread", "disk_read", 0.85)]
    [InlineData("netbytes", "net_bytes", 0.88)]
    public void JaroWinkler_NearMatches_ShouldScoreHighly(
        string a, string b, double minScore)
    {
        var score = AttributeNormalizationService.JaroWinkler(a, b);
        score.Should().BeGreaterThan(minScore,
            because: $"'{a}' and '{b}' are near-matches and should score > {minScore}");
    }

    [Theory]
    [InlineData("cpu_percent", "disk_write_mbps")]
    [InlineData("memory_used", "network_packets_out")]
    [InlineData("error_rate", "cpu_temperature")]
    public void JaroWinkler_UnrelatedAttributes_ShouldScoreLow(string a, string b)
    {
        var score = AttributeNormalizationService.JaroWinkler(a, b);
        score.Should().BeLessThan(0.75,
            because: $"'{a}' and '{b}' are unrelated and should not match");
    }

    [Fact]
    public void JaroWinkler_EmptyStrings_ShouldReturnZero()
    {
        AttributeNormalizationService.JaroWinkler("", "cpu_percent").Should().Be(0.0);
        AttributeNormalizationService.JaroWinkler("cpu_percent", "").Should().Be(0.0);
        AttributeNormalizationService.JaroWinkler("", "").Should().Be(0.0);
    }

    [Fact]
    public void JaroWinkler_IsSymmetric()
    {
        double forward = AttributeNormalizationService.JaroWinkler("cpu_percent", "processor_usage");
        double backward = AttributeNormalizationService.JaroWinkler("processor_usage", "cpu_percent");
        forward.Should().BeApproximately(backward, precision: 0.001);
    }

    [Theory]
    [InlineData("Name", "name", true)]
    [InlineData("Full Name", "name", false)]
    [InlineData("Surname", "name", false)]
    [InlineData("CPU %", "cpu_percent", true)]
    [InlineData("cpu usage", "cpu_percent", true)]
    [InlineData("MemUsage", "memory_percent", true)]
    public void NormaliseKey_RealWorldRenameScenarios(
        string rawName, string canonicalName, bool shouldNormaliseSimilarly)
    {
        var normRaw = AttributeNormalizationService.NormaliseKey(rawName);
        var normCanonical = AttributeNormalizationService.NormaliseKey(canonicalName);

        if (shouldNormaliseSimilarly)
            normRaw.Should().Be(normCanonical,
                because: $"'{rawName}' should normalise to the same key as '{canonicalName}'");
        else
            normRaw.Should().NotBe(normCanonical,
                because: $"'{rawName}' and '{canonicalName}' are different concepts");
    }
}
