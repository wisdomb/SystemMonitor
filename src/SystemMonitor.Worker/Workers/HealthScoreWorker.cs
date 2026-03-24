using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Worker.Workers;

public class HealthScoreWorker : BackgroundService
{
    private readonly CosmosClient _cosmos;
    private readonly IConfiguration _cfg;
    private readonly ILogger<HealthScoreWorker> _logger;

    public HealthScoreWorker(
        CosmosClient cosmos,
        IConfiguration cfg,
        ILogger<HealthScoreWorker> logger)
    {
        _cosmos = cosmos;
        _cfg = cfg;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _cosmos.GetDatabase(_cfg["CosmosDb:Database"]);
        var metricsContainer = db.GetContainer("metrics");
        var anomalyContainer = db.GetContainer("anomalies");

        var hub = new HubConnectionBuilder()
            .WithUrl(_cfg["SignalR:HubUrl"]!)
            .WithAutomaticReconnect()
            .Build();

        try
        {
            await hub.StartAsync(stoppingToken);
            _logger.LogInformation("HealthScoreWorker connected to SignalR hub");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not connect to SignalR hub on startup — will retry each tick");
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested
                && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                if (hub.State == HubConnectionState.Disconnected)
                {
                    try { await hub.StartAsync(stoppingToken); }
                    catch { }
                }

                var scores = await ComputeHealthScoresAsync(
                    metricsContainer, anomalyContainer, stoppingToken);

                foreach (var (agentId, score) in scores)
                {
                    if (hub.State == HubConnectionState.Connected)
                    {
                        await hub.InvokeAsync("BroadcastHealthScore",
                            new { agentId, score, timestamp = DateTimeOffset.UtcNow },
                            stoppingToken);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health score computation failed");
            }
        }

        await hub.StopAsync();
    }

    private async Task<Dictionary<string, double>> ComputeHealthScoresAsync(
        Container metrics, Container anomalies, CancellationToken ct)
    {
        var since = DateTimeOffset.UtcNow.AddMinutes(-5);
        var scores = new Dictionary<string, double>();

        var metricQuery = metrics.GetItemLinqQueryable<MetricEvent>()
            .Where(m => m.Timestamp >= since)
            .ToFeedIterator();

        var recentMetrics = new List<MetricEvent>();
        while (metricQuery.HasMoreResults)
        {
            var page = await metricQuery.ReadNextAsync(ct);
            recentMetrics.AddRange(page);
        }

        foreach (var agentId in recentMetrics.Select(m => m.AgentId).Distinct())
        {
            var agentMetrics = recentMetrics.Where(m => m.AgentId == agentId).ToList();
            double score = 100;

            var cpuVals = agentMetrics
                .Where(m => m.Values.ContainsKey("cpu_percent"))
                .Select(m => m.Values["cpu_percent"]).ToList();
            if (cpuVals.Any())
            {
                var cpuAvg = cpuVals.Average();
                if (cpuAvg > 90) score -= 15;
                else if (cpuAvg > 75) score -= 5;
            }

            var memVals = agentMetrics
                .Where(m => m.Values.ContainsKey("memory_percent"))
                .Select(m => m.Values["memory_percent"]).ToList();
            if (memVals.Any())
            {
                var memAvg = memVals.Average();
                if (memAvg > 90) score -= 10;
                else if (memAvg > 80) score -= 4;
            }

            var errVals = agentMetrics
                .Where(m => m.Values.ContainsKey("error_rate"))
                .Select(m => m.Values["error_rate"]).ToList();
            if (errVals.Any())
                score -= errVals.Average() * 30;

            var anomalyQuery = anomalies.GetItemLinqQueryable<AnomalyResult>(
                requestOptions: new QueryRequestOptions
                { PartitionKey = new PartitionKey(agentId) })
                .Where(a => a.AgentId == agentId && a.DetectedAt >= since)
                .ToFeedIterator();

            int anomalyCount = 0;
            while (anomalyQuery.HasMoreResults)
            {
                var page = await anomalyQuery.ReadNextAsync(ct);
                anomalyCount += page.Count;
            }

            score -= anomalyCount * 10;
            scores[agentId] = Math.Max(0, Math.Min(100, score));
        }

        return scores;
    }
}