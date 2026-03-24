using System.Text.Json;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Services;

public interface IAiServiceClient
{
    Task<List<AnomalyResult>> AnalyzeMetricsAsync(
        IReadOnlyList<MetricEvent> events, CancellationToken ct);
    Task TriggerTrainingAsync(
        IReadOnlyList<TrainingDataRecord> records, CancellationToken ct);
}

public class AiServiceClient : IAiServiceClient
{
    private readonly HttpClient _http;
    private readonly ILogger<AiServiceClient> _logger;

    private static readonly JsonSerializerOptions Opts = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public AiServiceClient(HttpClient http, ILogger<AiServiceClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<List<AnomalyResult>> AnalyzeMetricsAsync(
        IReadOnlyList<MetricEvent> events, CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/analyze-metrics", events, Opts, ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<List<AnomalyResult>>(Opts, ct)
                    ?? new();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service analyze call failed");
            return new();
        }
    }

    public async Task TriggerTrainingAsync(
        IReadOnlyList<TrainingDataRecord> records, CancellationToken ct)
    {
        try
        {
            var response = await _http.PostAsJsonAsync("/train", records, Opts, ct);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("AI service training triggered with {Count} records", records.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI service training call failed");
        }
    }
}
