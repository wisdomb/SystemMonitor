using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SystemMonitor.Agent.Generators;
using SystemMonitor.Agent.Senders;

namespace SystemMonitor.Agent;

public class AgentWorker : BackgroundService
{
    private readonly MockMetricGenerator _mockMetrics;
    private readonly MockLogGenerator _mockLogs;
    private readonly TelemetrySender _sender;
    private readonly IConfiguration _globalCfg;
    private readonly AgentConfig _agentCfg;
    private readonly ILogger<AgentWorker> _logger;

    public AgentWorker(
        MockMetricGenerator mockMetrics,
        MockLogGenerator mockLogs,
        TelemetrySender sender,
        IConfiguration globalCfg,
        AgentConfig agentCfg,
        ILogger<AgentWorker> logger)
    {
        _mockMetrics = mockMetrics;
        _mockLogs = mockLogs;
        _sender = sender;
        _globalCfg = globalCfg;
        _agentCfg = agentCfg;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        bool mockEnabled = _globalCfg.GetValue<bool>("MockData:Enabled", true);
        double anomalyProb = _agentCfg.AnomalyProbability
                           ?? _globalCfg.GetValue<double>("MockData:AnomalyProbability", 0.15);
        double logBurstProb = _globalCfg.GetValue<double>("MockData:LogBurstProbability", 0.10);
        int collectSecs = _agentCfg.CollectIntervalSeconds;
        int flushSecs = _agentCfg.FlushIntervalSeconds;

        _logger.LogInformation(
            "Agent {Id} ({Host}) started in {Mode} mode — collecting every {C}s, flushing every {F}s",
            _agentCfg.Id, _agentCfg.HostName,
            mockEnabled ? "MOCK" : "REAL",
            collectSecs, flushSecs);

        using var collectTimer = new PeriodicTimer(TimeSpan.FromSeconds(collectSecs));
        using var flushTimer = new PeriodicTimer(TimeSpan.FromSeconds(flushSecs));

        await Task.WhenAll(
            CollectLoop(collectTimer, mockEnabled, anomalyProb, logBurstProb, stoppingToken),
            FlushLoop(flushTimer, stoppingToken)
        );
    }

    private async Task CollectLoop(
        PeriodicTimer timer, bool mock,
        double anomalyProb, double logBurstProb,
        CancellationToken ct)
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                if (mock)
                {
                    var metric = _mockMetrics.Generate(
                        _agentCfg.Id, _agentCfg.HostName, anomalyProb);
                    _sender.EnqueueMetric(metric);

                    var logs = _mockLogs.Generate(
                        _agentCfg.Id, _agentCfg.HostName, logBurstProb);
                    foreach (var l in logs) _sender.EnqueueLog(l);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Collection error for agent {Id}", _agentCfg.Id);
            }
        }
    }

    private async Task FlushLoop(PeriodicTimer timer, CancellationToken ct)
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await _sender.FlushAsync(_agentCfg.Id, _agentCfg.HostName, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Flush error for agent {Id}", _agentCfg.Id);
            }
        }
    }
}

public class AgentConfig
{
    public string Id { get; set; } = "agent-001";
    public string HostName { get; set; } = "localhost";
    public string Environment { get; set; } = "production";
    public int CollectIntervalSeconds { get; set; } = 10;
    public int FlushIntervalSeconds { get; set; } = 30;
    public double? AnomalyProbability { get; set; } = null;
}