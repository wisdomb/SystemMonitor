using Microsoft.AspNetCore.Mvc;
using SystemMonitor.AiService.Services;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.AiService.Controllers;

[ApiController]
[Route("/")]
public class AnalyzeController : ControllerBase
{
    private readonly AnomalyDetectionService _detector;
    private readonly ModelTrainerService _trainer;
    private readonly ILogger<AnalyzeController> _logger;

    public AnalyzeController(
        AnomalyDetectionService detector,
        ModelTrainerService trainer,
        ILogger<AnalyzeController> logger)
    {
        _detector = detector;
        _trainer = trainer;
        _logger = logger;
    }

    [HttpPost("analyze-metrics")]
    [ProducesResponseType(typeof(List<AnomalyResult>), StatusCodes.Status200OK)]
    public IActionResult AnalyzeMetrics([FromBody] List<MetricEvent> events)
    {
        if (events is null || events.Count == 0)
            return Ok(Array.Empty<AnomalyResult>());

        var results = _detector.AnalyzeMetrics(events);
        _logger.LogDebug("Analyzed {EventCount} events → {AnomalyCount} anomalies",
            events.Count, results.Count);
        return Ok(results);
    }

    [HttpPost("analyze-metric")]
    public IActionResult AnalyzeSingleMetric([FromBody] MetricEvent evt)
        => AnalyzeMetrics(new List<MetricEvent> { evt });

    [HttpPost("train")]
    public async Task<IActionResult> Train(
        [FromBody] List<TrainingDataRecord> records,
        CancellationToken ct)
    {
        if (records.Count < 50)
            return BadRequest("At least 50 training records are required.");

        await _trainer.TrainAsync(records, ct);
        return Ok(new { trained = records.Count });
    }

    [HttpGet("health")]
    public IActionResult Health() => Ok(new { status = "ok" });
}
