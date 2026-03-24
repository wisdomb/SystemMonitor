using System.Net.Http;
using System.Text.Json;
using System.Threading.Channels;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Agent.Senders;

public class TelemetrySender
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<TelemetrySender> _logger;

    private readonly Channel<MetricEvent> _metricChannel;
    private readonly Channel<LogEvent> _logChannel;
    private static readonly JsonSerializerOptions JsonOpts = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public TelemetrySender(
        IHttpClientFactory httpFactory,
        IConfiguration cfg,
        ILogger<TelemetrySender> logger)
    {
        _httpFactory = httpFactory;
        _cfg = cfg;
        _logger = logger;

        var opts = new BoundedChannelOptions(10_000)
        { FullMode = BoundedChannelFullMode.DropOldest };

        _metricChannel = Channel.CreateBounded<MetricEvent>(opts);
        _logChannel = Channel.CreateBounded<LogEvent>(opts);
    }

    public void EnqueueMetric(MetricEvent evt)
        => _metricChannel.Writer.TryWrite(evt);

    public void EnqueueLog(LogEvent evt)
        => _logChannel.Writer.TryWrite(evt);

    public async Task FlushAsync(string agentId, string hostName, CancellationToken ct)
    {
        await FlushMetricsAsync(ct);
        await FlushLogsAsync(ct);
    }

    private async Task FlushMetricsAsync(CancellationToken ct)
    {
        var batch = new List<MetricEvent>();
        while (batch.Count < 200 && _metricChannel.Reader.TryRead(out var evt))
            batch.Add(evt);

        if (batch.Count == 0) return;

        var ingestionBatch = new IngestionBatch<MetricEvent> { Events = batch };
        await PostWithRetryAsync("api/v1/ingest/metrics", ingestionBatch, ct);
    }

    private async Task FlushLogsAsync(CancellationToken ct)
    {
        var batch = new List<LogEvent>();
        while (batch.Count < 500 && _logChannel.Reader.TryRead(out var evt))
            batch.Add(evt);

        if (batch.Count == 0) return;

        var ingestionBatch = new IngestionBatch<LogEvent> { Events = batch };
        await PostWithRetryAsync("api/v1/ingest/logs", ingestionBatch, ct);
    }

    private async Task PostWithRetryAsync<T>(string path, T payload, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient("ApiGateway");
        var content = new StringContent(
            JsonSerializer.Serialize(payload, JsonOpts),
            System.Text.Encoding.UTF8,
            "application/json");

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var response = await http.PostAsync(path, content, ct);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Flushed batch to {Path} (attempt {Attempt})", path, attempt);
                    return;
                }
                _logger.LogWarning("API returned {Status} on attempt {Attempt}", response.StatusCode, attempt);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "HTTP error on attempt {Attempt} posting to {Path}", attempt, path);
            }

            if (attempt < 3)
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
        }

        _logger.LogError("Failed to send batch to {Path} after 3 attempts — data dropped", path);
    }
}