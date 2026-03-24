using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Services;

public interface ISchemaRegistryRepository
{
    Task<IReadOnlyList<CanonicalAttribute>> GetAllCanonicalAttributesAsync(CancellationToken ct);
    Task<CanonicalAttribute?> GetCanonicalAttributeAsync(string id, CancellationToken ct);
    Task UpsertCanonicalAttributeAsync(CanonicalAttribute attribute, CancellationToken ct);
    Task UpdateCanonicalAttributeAsync(CanonicalAttribute attribute, CancellationToken ct);
    Task DeleteCanonicalAttributeAsync(string id, CancellationToken ct);
    Task QueueUnknownAttributeAsync(string rawName, string agentId, string sourceVersion, List<ResolutionCandidate> candidates, CancellationToken ct);
    Task<UnknownAttribute?> GetUnknownAttributeAsync(string id, CancellationToken ct);
    Task<IReadOnlyList<UnknownAttribute>> GetPendingUnknownAttributesAsync(CancellationToken ct);
    Task ConfirmMappingAsync(string id, string canonicalId, string reviewedBy, string? note, CancellationToken ct);
    Task RejectMappingAsync(string id, string reviewedBy, string? note, CancellationToken ct);
    Task SaveSnapshotAsync(SchemaSnapshot snapshot, CancellationToken ct);
    Task<SchemaSnapshot?> GetLatestSnapshotAsync(string agentId, CancellationToken ct);
}

public class SchemaRegistryRepository : ISchemaRegistryRepository
{
    private readonly Dictionary<string, CanonicalAttribute> _canonicals = new();
    private readonly Dictionary<string, UnknownAttribute> _unknowns = new();
    private readonly Dictionary<string, SchemaSnapshot> _snapshots = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    private readonly CosmosClient _cosmos;
    private readonly IConfiguration _cfg;
    private readonly ILogger<SchemaRegistryRepository> _logger;

    public SchemaRegistryRepository(
        CosmosClient cosmos,
        IConfiguration cfg,
        ILogger<SchemaRegistryRepository> logger)
    {
        _cosmos = cosmos;
        _cfg = cfg;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CanonicalAttribute>> GetAllCanonicalAttributesAsync(
        CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        lock (_canonicals)
            return _canonicals.Values.ToList();
    }

    public async Task<CanonicalAttribute?> GetCanonicalAttributeAsync(
        string id, CancellationToken ct)
    {
        await EnsureInitializedAsync(ct);
        lock (_canonicals)
            return _canonicals.TryGetValue(id, out var attr) ? attr : null;
    }

    public Task UpsertCanonicalAttributeAsync(CanonicalAttribute attribute, CancellationToken ct)
    {
        lock (_canonicals)
            _canonicals[attribute.Id] = attribute;

        _ = PersistCanonicalAsync(attribute);
        return Task.CompletedTask;
    }

    public Task UpdateCanonicalAttributeAsync(CanonicalAttribute attribute, CancellationToken ct)
        => UpsertCanonicalAttributeAsync(attribute, ct);

    public Task DeleteCanonicalAttributeAsync(string id, CancellationToken ct)
    {
        lock (_canonicals) _canonicals.Remove(id);
        return Task.CompletedTask;
    }

    public Task QueueUnknownAttributeAsync(
        string rawName, string agentId, string sourceVersion,
        List<ResolutionCandidate> candidates, CancellationToken ct)
    {
        lock (_unknowns)
        {
            var existing = _unknowns.Values
                .FirstOrDefault(u => u.RawName == rawName
                    && u.Status == UnknownAttributeStatus.Pending);

            if (existing is not null)
            {
                existing.OccurrenceCount++;
                return Task.CompletedTask;
            }

            var best = candidates.OrderByDescending(c => c.Score).FirstOrDefault();
            var unknown = new UnknownAttribute
            {
                RawName = rawName,
                SourceAgentId = agentId,
                SourceVersion = sourceVersion,
                SuggestedCanonicalId = best?.CanonicalId,
                SuggestionConfidence = best?.Score ?? 0,
                SuggestionMethod = best?.Method ?? ResolutionMethod.Unresolved,
                Candidates = candidates,
                Status = UnknownAttributeStatus.Pending
            };
            _unknowns[unknown.Id] = unknown;
        }

        return Task.CompletedTask;
    }

    public Task<UnknownAttribute?> GetUnknownAttributeAsync(string id, CancellationToken ct)
    {
        lock (_unknowns)
            return Task.FromResult(
                _unknowns.TryGetValue(id, out var u) ? u : null);
    }

    public Task<IReadOnlyList<UnknownAttribute>> GetPendingUnknownAttributesAsync(
        CancellationToken ct)
    {
        lock (_unknowns)
        {
            var results = _unknowns.Values
                .Where(u => u.Status == UnknownAttributeStatus.Pending)
                .OrderByDescending(u => u.OccurrenceCount)
                .ToList();
            return Task.FromResult<IReadOnlyList<UnknownAttribute>>(results);
        }
    }

    public Task ConfirmMappingAsync(
        string id, string canonicalId, string reviewedBy, string? note, CancellationToken ct)
    {
        lock (_unknowns)
        {
            if (!_unknowns.TryGetValue(id, out var item)) return Task.CompletedTask;
            item.Status = UnknownAttributeStatus.Confirmed;
            item.ConfirmedCanonicalId = canonicalId;
            item.ReviewedBy = reviewedBy;
            item.ReviewedAt = DateTimeOffset.UtcNow;
            item.ReviewNote = note;
        }
        return Task.CompletedTask;
    }

    public Task RejectMappingAsync(
        string id, string reviewedBy, string? note, CancellationToken ct)
    {
        lock (_unknowns)
        {
            if (!_unknowns.TryGetValue(id, out var item)) return Task.CompletedTask;
            item.Status = UnknownAttributeStatus.Rejected;
            item.ReviewedBy = reviewedBy;
            item.ReviewedAt = DateTimeOffset.UtcNow;
            item.ReviewNote = note;
        }
        return Task.CompletedTask;
    }

    public Task SaveSnapshotAsync(SchemaSnapshot snapshot, CancellationToken ct)
    {
        lock (_snapshots)
            _snapshots[snapshot.AgentId] = snapshot;
        return Task.CompletedTask;
    }

    public Task<SchemaSnapshot?> GetLatestSnapshotAsync(string agentId, CancellationToken ct)
    {
        lock (_snapshots)
            return Task.FromResult(
                _snapshots.TryGetValue(agentId, out var s) ? s : null);
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;

            lock (_canonicals)
            {
                foreach (var attr in CanonicalAttributeSeeder.GetSeedAttributes())
                    _canonicals[attr.Id] = attr;
            }

            _logger.LogInformation(
                "Schema registry initialized with {Count} canonical attributes from seeder",
                _canonicals.Count);

            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task PersistCanonicalAsync(CanonicalAttribute attribute)
    {
        try
        {
            var container = _cosmos
                .GetDatabase(_cfg["CosmosDb:Database"])
                .GetContainer("canonicalAttributes");
            await container.UpsertItemAsync(attribute, new PartitionKey(attribute.Id));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Background Cosmos persist failed for {Id}", attribute.Id);
        }
    }
}