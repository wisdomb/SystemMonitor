using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Services;

public interface ICosmosRepository
{
    Task UpsertMetricAsync(MetricEvent metric, CancellationToken ct);
    Task UpsertLogAsync(LogEvent log, CancellationToken ct);
    Task UpsertAnomalyAsync(AnomalyResult anomaly, CancellationToken ct);
    Task<IReadOnlyList<AnomalyResult>> QueryAnomaliesAsync(
        string? agentId, AnomalySeverity? minSeverity,
        DateTimeOffset since, int limit, CancellationToken ct);
    Task<IReadOnlyList<MetricEvent>> QueryMetricsAsync(
        string agentId, DateTimeOffset since, CancellationToken ct);
    Task<IReadOnlyList<MetricEvent>> QueryRecentMetricsAsync(
        DateTimeOffset since, CancellationToken ct);
}

public class CosmosRepository : ICosmosRepository
{
    private readonly Container _metricsContainer;
    private readonly Container _logsContainer;
    private readonly Container _anomaliesContainer;

    public CosmosRepository(CosmosClient client, IConfiguration cfg)
    {
        var db = client.GetDatabase(cfg["CosmosDb:Database"]);
        _metricsContainer = db.GetContainer("metrics");
        _logsContainer = db.GetContainer("logs");
        _anomaliesContainer = db.GetContainer("anomalies");
    }

    public async Task UpsertMetricAsync(MetricEvent metric, CancellationToken ct)
        => await _metricsContainer.UpsertItemAsync(
            metric, new PartitionKey(metric.AgentId), cancellationToken: ct);

    public async Task UpsertLogAsync(LogEvent log, CancellationToken ct)
        => await _logsContainer.UpsertItemAsync(
            log, new PartitionKey(log.AgentId), cancellationToken: ct);

    public async Task UpsertAnomalyAsync(AnomalyResult anomaly, CancellationToken ct)
        => await _anomaliesContainer.UpsertItemAsync(
            anomaly, new PartitionKey(anomaly.AgentId), cancellationToken: ct);

    public async Task<IReadOnlyList<AnomalyResult>> QueryAnomaliesAsync(
        string? agentId, AnomalySeverity? minSeverity,
        DateTimeOffset since, int limit, CancellationToken ct)
    {
        var sinceStr = since.ToString("O");
        var sql = $"SELECT TOP {limit} * FROM c WHERE c.detectedAt >= '{sinceStr}'";

        if (!string.IsNullOrEmpty(agentId))
            sql = $"SELECT TOP {limit} * FROM c WHERE c.detectedAt >= '{sinceStr}' AND c.agentId = '{agentId}'";

        var query = new QueryDefinition(sql);
        var opts = new QueryRequestOptions
        {
            MaxItemCount = limit,
            MaxConcurrency = 1
        };

        var feed = _anomaliesContainer.GetItemQueryIterator<AnomalyResult>(query, requestOptions: opts);
        var results = new List<AnomalyResult>();

        while (feed.HasMoreResults && results.Count < limit)
        {
            try
            {
                var page = await feed.ReadNextAsync(ct);
                results.AddRange(page);
            }
            catch { break; }
        }

        return results;
    }

    public async Task<IReadOnlyList<MetricEvent>> QueryMetricsAsync(
        string agentId, DateTimeOffset since, CancellationToken ct)
    {
        var sinceStr = since.ToString("O");
        var sql = $"SELECT * FROM c WHERE c.agentId = '{agentId}' AND c.timestamp >= '{sinceStr}' ORDER BY c.timestamp";

        var feed = _metricsContainer.GetItemQueryIterator<MetricEvent>(
            new QueryDefinition(sql),
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(agentId),
                MaxItemCount = 500
            });

        var results = new List<MetricEvent>();
        while (feed.HasMoreResults)
        {
            try
            {
                var page = await feed.ReadNextAsync(ct);
                results.AddRange(page);
            }
            catch { break; }
        }
        return results;
    }

    public async Task<IReadOnlyList<MetricEvent>> QueryRecentMetricsAsync(
        DateTimeOffset since, CancellationToken ct)
    {
        var sinceStr = since.ToString("O");

        var sql = $"SELECT TOP 20 c.agentId, c.timestamp, c.values FROM c WHERE c.timestamp >= '{sinceStr}'";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var feed = _metricsContainer.GetItemQueryIterator<MetricEvent>(
            new QueryDefinition(sql),
            requestOptions: new QueryRequestOptions
            {
                MaxItemCount = 20,
                MaxConcurrency = 1
            });

        var results = new List<MetricEvent>();
        try
        {
            while (feed.HasMoreResults && results.Count < 20)
            {
                var page = await feed.ReadNextAsync(cts.Token);
                results.AddRange(page);
                break;
            }
        }
        catch (OperationCanceledException)
        { }
        catch
        { }

        return results;
    }
}