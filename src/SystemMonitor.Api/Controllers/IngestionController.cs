using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using SystemMonitor.Api.Hubs;
using SystemMonitor.Api.Services;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Controllers;

[ApiController]
[Route("api/v1/ingest")]
public class IngestionController : ControllerBase
{
    private readonly IIngestionService _ingestion;
    private readonly IHubContext<MonitoringHub> _hub;
    private readonly ILogger<IngestionController> _logger;

    public IngestionController(
        IIngestionService ingestion,
        IHubContext<MonitoringHub> hub,
        ILogger<IngestionController> logger)
    {
        _ingestion = ingestion;
        _hub = hub;
        _logger = logger;
    }

    [HttpPost("metrics")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> IngestMetrics(
        [FromBody] IngestionBatch<MetricEvent> batch,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "Received metric batch {BatchId} with {Count} events",
            batch.BatchId, batch.Events.Count);

        await _ingestion.EnqueueMetricsAsync(batch, ct);

        await _hub.Clients.All.SendAsync(
            "MetricBatchReceived",
            new { batch.BatchId, Count = batch.Events.Count, ReceivedAt = DateTimeOffset.UtcNow },
            ct);

        return Accepted(new { batch.BatchId, queued = batch.Events.Count });
    }

    [HttpPost("logs")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> IngestLogs(
        [FromBody] IngestionBatch<LogEvent> batch,
        CancellationToken ct)
    {
        await _ingestion.EnqueueLogsAsync(batch, ct);

        await _hub.Clients.All.SendAsync(
            "LogBatchReceived",
            new { batch.BatchId, Count = batch.Events.Count, ReceivedAt = DateTimeOffset.UtcNow },
            ct);

        return Accepted(new { batch.BatchId, queued = batch.Events.Count });
    }

    [HttpPost("training-data")]
    [Consumes("application/json", "text/csv")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> UploadTrainingData(CancellationToken ct)
    {
        var contentType = Request.ContentType ?? string.Empty;
        List<TrainingDataRecord> records;

        if (contentType.Contains("text/csv"))
        {
            using var reader = new StreamReader(Request.Body);
            var csv = await reader.ReadToEndAsync(ct);
            records = CsvParser.ParseTrainingData(csv);
        }
        else
        {
            records = await Request.ReadFromJsonAsync<List<TrainingDataRecord>>(ct)
                        ?? new List<TrainingDataRecord>();
        }

        if (records.Count == 0)
            return BadRequest("No valid training records found.");

        await _ingestion.EnqueueTrainingDataAsync(records, ct);

        _logger.LogInformation("Received {Count} training records", records.Count);
        return Accepted(new { ingested = records.Count });
    }

    [HttpPost("metric")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public Task<IActionResult> IngestSingleMetric(
        [FromBody] MetricEvent evt, CancellationToken ct)
    {
        var batch = new IngestionBatch<MetricEvent>
        { Events = new[] { evt } };
        return IngestMetrics(batch, ct);
    }
}
