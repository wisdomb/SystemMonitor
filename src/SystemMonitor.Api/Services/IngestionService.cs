using Azure.Messaging.ServiceBus;
using System.Text.Json;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Services;

public interface IIngestionService
{
    Task EnqueueMetricsAsync(IngestionBatch<MetricEvent> batch, CancellationToken ct);
    Task EnqueueLogsAsync(IngestionBatch<LogEvent> batch, CancellationToken ct);
    Task EnqueueTrainingDataAsync(IList<TrainingDataRecord> records, CancellationToken ct);
}

public class IngestionService : IIngestionService
{
    private readonly ServiceBusSender _metricSender;
    private readonly ServiceBusSender _logSender;
    private readonly ServiceBusSender _trainingSender;
    private readonly ILogger<IngestionService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public IngestionService(ServiceBusClient sbClient, ILogger<IngestionService> logger)
    {
        _metricSender = sbClient.CreateSender("metrics-queue");
        _logSender = sbClient.CreateSender("logs-queue");
        _trainingSender = sbClient.CreateSender("training-queue");
        _logger = logger;
    }

    public async Task EnqueueMetricsAsync(IngestionBatch<MetricEvent> batch, CancellationToken ct)
    {
        foreach (var chunk in Chunk(batch.Events, 100))
        {
            var msg = BuildMessage(new IngestionBatch<MetricEvent>
            {
                BatchId = batch.BatchId,
                ReceivedAt = batch.ReceivedAt,
                Events = chunk
            });
            msg.Subject = "metric-batch";
            await _metricSender.SendMessageAsync(msg, ct);
        }

        _logger.LogDebug("Enqueued {Count} metric events", batch.Events.Count);
    }

    public async Task EnqueueLogsAsync(IngestionBatch<LogEvent> batch, CancellationToken ct)
    {
        foreach (var chunk in Chunk(batch.Events, 200))
        {
            var msg = BuildMessage(new IngestionBatch<LogEvent>
            {
                BatchId = batch.BatchId,
                ReceivedAt = batch.ReceivedAt,
                Events = chunk
            });
            msg.Subject = "log-batch";
            await _logSender.SendMessageAsync(msg, ct);
        }
    }

    public async Task EnqueueTrainingDataAsync(IList<TrainingDataRecord> records, CancellationToken ct)
    {
        foreach (var chunk in Chunk(records.ToList(), 500))
        {
            var msg = BuildMessage(chunk);
            msg.Subject = "training-batch";
            await _trainingSender.SendMessageAsync(msg, ct);
        }

        _logger.LogInformation("Enqueued {Count} training records", records.Count);
    }

    private static ServiceBusMessage BuildMessage<T>(T payload)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts);
        return new ServiceBusMessage(body)
        {
            ContentType = "application/json",
            MessageId = Guid.NewGuid().ToString()
        };
    }

    private static IEnumerable<IReadOnlyList<T>> Chunk<T>(IReadOnlyList<T> source, int size)
    {
        for (int i = 0; i < source.Count; i += size)
            yield return source.Skip(i).Take(size).ToList();
    }
}