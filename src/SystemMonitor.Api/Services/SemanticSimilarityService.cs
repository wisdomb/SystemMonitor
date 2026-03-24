using System.Net.Http.Json;
using System.Text.Json;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Services;

public interface ISemanticSimilarityService
{
    Task<(CanonicalAttribute? best, double score)> FindBestMatchAsync(
        string rawName,
        IEnumerable<CanonicalAttribute> candidates,
        CancellationToken ct);

    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct);
}

public class SemanticSimilarityService : ISemanticSimilarityService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _cfg;
    private readonly ILogger<SemanticSimilarityService> _logger;

    private readonly Dictionary<string, float[]> _embeddingCache = new();
    private readonly SemaphoreSlim _cacheLock = new(1, 1);

    private bool _openAiAvailable = true;

    public SemanticSimilarityService(
        IHttpClientFactory httpFactory,
        IConfiguration cfg,
        ILogger<SemanticSimilarityService> logger)
    {
        _http = httpFactory.CreateClient("AzureOpenAI");
        _cfg = cfg;
        _logger = logger;
    }

    public async Task<(CanonicalAttribute? best, double score)> FindBestMatchAsync(
        string rawName,
        IEnumerable<CanonicalAttribute> candidates,
        CancellationToken ct)
    {
        var candidateList = candidates.ToList();
        if (candidateList.Count == 0) return (null, 0);

        float[] rawEmbedding;
        try
        {
            rawEmbedding = await GetEmbeddingAsync(rawName, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Embedding call failed for '{RawName}' — falling back to lexical", rawName);
            _openAiAvailable = false;
            return (null, 0);
        }

        CanonicalAttribute? bestCandidate = null;
        double bestScore = 0;

        foreach (var candidate in candidateList)
        {
            float[]? candidateEmbedding = candidate.EmbeddingVector;

            if (candidateEmbedding is null || candidateEmbedding.Length == 0)
            {
                try
                {
                    string descText = $"{candidate.DisplayName}: {candidate.Description}. " +
                                      $"Also known as: {string.Join(", ", candidate.KnownAliases.Take(5))}";
                    candidateEmbedding = await GetEmbeddingAsync(descText, ct);
                }
                catch
                {
                    continue;
                }
            }

            double similarity = CosineSimilarity(rawEmbedding, candidateEmbedding);
            if (similarity > bestScore)
            {
                bestScore = similarity;
                bestCandidate = candidate;
            }
        }

        return (bestCandidate, bestScore);
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        await _cacheLock.WaitAsync(ct);
        try
        {
            if (_embeddingCache.TryGetValue(text, out var cached))
                return cached;
        }
        finally
        {
            _cacheLock.Release();
        }

        if (!_openAiAvailable)
            return Array.Empty<float>();

        var endpoint = _cfg["AzureOpenAI:Endpoint"];
        var apiKey = _cfg["AzureOpenAI:ApiKey"];
        var deployment = _cfg["AzureOpenAI:EmbeddingDeployment"] ?? "text-embedding-3-small";

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            _openAiAvailable = false;
            return Array.Empty<float>();
        }

        var requestBody = new { input = text, model = deployment };
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{endpoint}/openai/deployments/{deployment}/embeddings?api-version=2024-02-01")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("api-key", apiKey);

        var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        var embeddingData = doc.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding");

        var vector = embeddingData.EnumerateArray()
            .Select(e => e.GetSingle())
            .ToArray();

        await _cacheLock.WaitAsync(ct);
        try { _embeddingCache[text] = vector; }
        finally { _cacheLock.Release(); }

        return vector;
    }

    // ── Maths ─────────────────────────────────────────────────────────────────

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0;

        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        double denom = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denom < 1e-10 ? 0 : dot / denom;
    }
}
