using System.Threading.Channels;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Shared;

public static class InMemoryQueues
{
    public static readonly Channel<IngestionBatch<MetricEvent>> Metrics =
        Channel.CreateBounded<IngestionBatch<MetricEvent>>(
            new BoundedChannelOptions(5_000)
            { FullMode = BoundedChannelFullMode.DropOldest });

    public static readonly Channel<IngestionBatch<LogEvent>> Logs =
        Channel.CreateBounded<IngestionBatch<LogEvent>>(
            new BoundedChannelOptions(5_000)
            { FullMode = BoundedChannelFullMode.DropOldest });

    public static readonly Channel<List<TrainingDataRecord>> Training =
        Channel.CreateBounded<List<TrainingDataRecord>>(
            new BoundedChannelOptions(100)
            { FullMode = BoundedChannelFullMode.Wait });
}