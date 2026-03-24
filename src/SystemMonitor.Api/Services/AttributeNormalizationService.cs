using System.Text.RegularExpressions;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api.Services;

public interface IAttributeNormalizationService
{
    Task<AttributeResolutionResult> ResolveAsync(
        string rawName, string agentId, string sourceVersion, CancellationToken ct = default);

    Task<NormalisedDataBlock> NormaliseAsync(
        string agentId, string hostName,
        Dictionary<string, object> rawFields,
        string sourceVersion = "unknown",
        CancellationToken ct = default);

    Task ConfirmMappingAsync(
        string unknownAttributeId, string canonicalId,
        string reviewedBy, string? note, CancellationToken ct);

    Task RejectMappingAsync(
        string unknownAttributeId, string reviewedBy, string? note, CancellationToken ct);
}

public class AttributeNormalizationService : IAttributeNormalizationService
{
    private readonly ISchemaRegistryRepository _registry;
    private readonly ISemanticSimilarityService _semantic;
    private readonly ILogger<AttributeNormalizationService> _logger;

    private const double AutoAcceptThreshold = 0.80;
    private const double QueueForReviewThreshold = 0.40;

    public AttributeNormalizationService(
        ISchemaRegistryRepository registry,
        ISemanticSimilarityService semantic,
        ILogger<AttributeNormalizationService> logger)
    {
        _registry = registry;
        _semantic = semantic;
        _logger = logger;
    }


    public async Task<AttributeResolutionResult> ResolveAsync(
        string rawName, string agentId, string sourceVersion, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawName))
            return Unresolved(rawName);

        var canonicals = await _registry.GetAllCanonicalAttributesAsync(ct);

        var exact = canonicals.FirstOrDefault(c =>
            string.Equals(c.Name, rawName, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
            return Resolved(rawName, exact, 1.0, ResolutionMethod.ExactMatch);

        var alias = canonicals.FirstOrDefault(c =>
            c.KnownAliases.Any(a => string.Equals(a, rawName, StringComparison.OrdinalIgnoreCase)));
        if (alias is not null)
            return Resolved(rawName, alias, 1.0, ResolutionMethod.AliasMatch);

        string normRaw = NormaliseKey(rawName);
        var normMatch = canonicals.FirstOrDefault(c => NormaliseKey(c.Name) == normRaw
            || c.KnownAliases.Any(a => NormaliseKey(a) == normRaw));
        if (normMatch is not null)
            return Resolved(rawName, normMatch, 0.95, ResolutionMethod.FuzzyMatch);

        var tokens = Tokenise(rawName);
        var (tokenBest, tokenScore) = canonicals
            .Select(c => (c, score: JaccardSimilarity(tokens, Tokenise(c.Name))))
            .OrderByDescending(x => x.score)
            .FirstOrDefault();

        if (tokenScore >= AutoAcceptThreshold)
            return Resolved(rawName, tokenBest!, tokenScore, ResolutionMethod.FuzzyMatch);

        var (jwBest, jwScore) = canonicals
            .SelectMany(c =>
            {
                var names = c.KnownAliases.Append(c.Name);
                double best = names.Max(n => JaroWinkler(rawName.ToLowerInvariant(), n.ToLowerInvariant()));
                return new[] { (c, best) };
            })
            .OrderByDescending(x => x.Item2)
            .FirstOrDefault();

        if (jwScore >= AutoAcceptThreshold)
            return Resolved(rawName, jwBest!, jwScore, ResolutionMethod.FuzzyMatch);

        if (jwScore >= QueueForReviewThreshold)
        {
            var (semBest, semScore) = await _semantic.FindBestMatchAsync(rawName, canonicals, ct);
            if (semBest is not null && semScore >= AutoAcceptThreshold)
                return Resolved(rawName, semBest, semScore, ResolutionMethod.SemanticMatch);

            var candidates = BuildCandidates(rawName, canonicals, jwBest, jwScore, semBest, semScore);
            await _registry.QueueUnknownAttributeAsync(
                rawName, agentId, sourceVersion, candidates, ct);

            _logger.LogWarning(
                "Attribute '{RawName}' from agent {AgentId} v{Version} could not be auto-resolved " +
                "(best: '{BestCanonical}' @ {Score:P0}). Queued for review.",
                rawName, agentId, sourceVersion,
                jwBest?.Name ?? "none", Math.Max(jwScore, semScore));
        }
        else
        {
            await _registry.QueueUnknownAttributeAsync(
                rawName, agentId, sourceVersion, new(), ct);
            _logger.LogWarning(
                "Completely unknown attribute '{RawName}' from {AgentId}. Queued for review.",
                rawName, agentId);
        }

        return Unresolved(rawName);
    }

    public async Task<NormalisedDataBlock> NormaliseAsync(
        string agentId, string hostName,
        Dictionary<string, object> rawFields,
        string sourceVersion = "unknown",
        CancellationToken ct = default)
    {
        var resolved = new Dictionary<string, object>();
        var unresolved = new Dictionary<string, object>();
        var audit = new Dictionary<string, AttributeResolutionResult>();

        await DetectSchemaDriftAsync(agentId, sourceVersion, rawFields.Keys.ToHashSet(), ct);

        foreach (var (rawKey, rawValue) in rawFields)
        {
            var result = await ResolveAsync(rawKey, agentId, sourceVersion, ct);
            audit[rawKey] = result;

            if (result.IsResolved)
                resolved[result.Resolved!.Name] = rawValue;
            else
                unresolved[rawKey] = rawValue;
        }

        if (unresolved.Count > 0)
            _logger.LogInformation(
                "Agent {AgentId}: {Resolved}/{Total} fields resolved, {Unresolved} queued for review",
                agentId, resolved.Count, rawFields.Count, unresolved.Count);

        return new NormalisedDataBlock
        {
            AgentId = agentId,
            HostName = hostName,
            Fields = resolved,
            UnresolvedFields = unresolved,
            ResolutionAudit = audit
        };
    }


    public async Task ConfirmMappingAsync(
        string unknownAttributeId, string canonicalId,
        string reviewedBy, string? note, CancellationToken ct)
    {
        await _registry.ConfirmMappingAsync(unknownAttributeId, canonicalId, reviewedBy, note, ct);
        var unknown = await _registry.GetUnknownAttributeAsync(unknownAttributeId, ct);
        var canonical = await _registry.GetCanonicalAttributeAsync(canonicalId, ct);

        if (unknown is not null && canonical is not null)
        {
            var updatedAliases = canonical.KnownAliases
                .Append(unknown.RawName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            await _registry.UpdateCanonicalAttributeAsync(
                canonical with { KnownAliases = updatedAliases, UpdatedAt = DateTimeOffset.UtcNow },
                ct);

            _logger.LogInformation(
                "Mapping confirmed: '{Raw}' → '{Canonical}' by {ReviewedBy}. Alias added.",
                unknown.RawName, canonical.Name, reviewedBy);
        }
    }

    public async Task RejectMappingAsync(
        string unknownAttributeId, string reviewedBy, string? note, CancellationToken ct)
        => await _registry.RejectMappingAsync(unknownAttributeId, reviewedBy, note, ct);

    private async Task DetectSchemaDriftAsync(
        string agentId, string sourceVersion,
        HashSet<string> currentNames, CancellationToken ct)
    {
        var lastSnapshot = await _registry.GetLatestSnapshotAsync(agentId, ct);
        if (lastSnapshot is null)
        {
            await _registry.SaveSnapshotAsync(new SchemaSnapshot
            {
                AgentId = agentId,
                SourceVersion = sourceVersion,
                AttributeNames = currentNames
            }, ct);
            return;
        }

        var disappeared = lastSnapshot.AttributeNames.Except(currentNames, StringComparer.OrdinalIgnoreCase).ToList();
        var appeared = currentNames.Except(lastSnapshot.AttributeNames, StringComparer.OrdinalIgnoreCase).ToList();

        if (disappeared.Count == 0 && appeared.Count == 0) return;

        _logger.LogWarning(
            "Schema drift detected for agent {AgentId} (version {OldVer} → {NewVer}): " +
            "{Disappeared} attribute(s) removed, {Appeared} new. " +
            "Possible renames: [{Renames}]",
            agentId,
            lastSnapshot.SourceVersion, sourceVersion,
            disappeared.Count, appeared.Count,
            string.Join(", ", GuessRenames(disappeared, appeared).Select(r => $"{r.old}→{r.newName}")));

        await _registry.SaveSnapshotAsync(new SchemaSnapshot
        {
            AgentId = agentId,
            SourceVersion = sourceVersion,
            AttributeNames = currentNames
        }, ct);
    }

    private static IEnumerable<(string old, string newName)> GuessRenames(
        IList<string> disappeared, IList<string> appeared)
    {
        foreach (var d in disappeared)
        {
            var best = appeared
                .Select(a => (a, score: JaroWinkler(
                    NormaliseKey(d), NormaliseKey(a))))
                .OrderByDescending(x => x.score)
                .FirstOrDefault();

            if (best.score >= 0.60)
                yield return (d, best.a);
        }
    }

    public static double JaroWinkler(string s1, string s2)
    {
        if (s1 == s2) return 1.0;
        if (s1.Length == 0 || s2.Length == 0) return 0.0;

        int matchWindow = Math.Max(s1.Length, s2.Length) / 2 - 1;
        if (matchWindow < 0) matchWindow = 0;

        bool[] s1Matches = new bool[s1.Length];
        bool[] s2Matches = new bool[s2.Length];
        int matches = 0, transpositions = 0;

        for (int i = 0; i < s1.Length; i++)
        {
            int start = Math.Max(0, i - matchWindow);
            int end = Math.Min(i + matchWindow + 1, s2.Length);

            for (int j = start; j < end; j++)
            {
                if (s2Matches[j] || s1[i] != s2[j]) continue;
                s1Matches[i] = s2Matches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0.0;

        int k = 0;
        for (int i = 0; i < s1.Length; i++)
        {
            if (!s1Matches[i]) continue;
            while (!s2Matches[k]) k++;
            if (s1[i] != s2[k]) transpositions++;
            k++;
        }

        double jaro = (
            (double)matches / s1.Length +
            (double)matches / s2.Length +
            (matches - transpositions / 2.0) / matches) / 3.0;

        int prefix = 0;
        for (int i = 0; i < Math.Min(4, Math.Min(s1.Length, s2.Length)); i++)
        {
            if (s1[i] == s2[i]) prefix++;
            else break;
        }

        return jaro + prefix * 0.1 * (1 - jaro);
    }

    public static string NormaliseKey(string name)
    {
        var expanded = Regex.Replace(name, @"([a-z])([A-Z])", "$1 $2")
                            .Replace("_", " ")
                            .Replace("-", " ")
                            .ToLowerInvariant();

        return Regex.Replace(expanded, @"[^a-z0-9\s]", "")
                    .Trim()
                    .Replace(" ", "");
    }

    private static HashSet<string> Tokenise(string name)
    {
        var expanded = Regex.Replace(name, @"([a-z])([A-Z])", "$1 $2")
                            .Replace("_", " ").Replace("-", " ")
                            .ToLowerInvariant();
        return Regex.Split(expanded, @"\s+")
                    .Where(t => t.Length > 1)
                    .ToHashSet();
    }

    private static double JaccardSimilarity(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 && b.Count == 0) return 1.0;
        int intersection = a.Count(x => b.Contains(x));
        int union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }

    private static AttributeResolutionResult Resolved(
        string rawName, CanonicalAttribute canonical, double confidence, ResolutionMethod method)
        => new()
        {
            RawName = rawName,
            Resolved = canonical,
            Confidence = confidence,
            Method = method,
            IsConfirmed = method is ResolutionMethod.ExactMatch
                            or ResolutionMethod.AliasMatch
                            or ResolutionMethod.ManualConfirm
        };

    private static AttributeResolutionResult Unresolved(string rawName)
        => new() { RawName = rawName, Method = ResolutionMethod.Unresolved };

    private static List<ResolutionCandidate> BuildCandidates(
        string rawName,
        IEnumerable<CanonicalAttribute> canonicals,
        CanonicalAttribute? jwBest, double jwScore,
        CanonicalAttribute? semBest, double semScore)
    {
        var candidates = new List<ResolutionCandidate>();
        if (jwBest is not null) candidates.Add(new() { CanonicalId = jwBest.Id, CanonicalName = jwBest.Name, Score = jwScore, Method = ResolutionMethod.FuzzyMatch });
        if (semBest is not null) candidates.Add(new() { CanonicalId = semBest.Id, CanonicalName = semBest.Name, Score = semScore, Method = ResolutionMethod.SemanticMatch });
        return candidates.DistinctBy(c => c.CanonicalId).OrderByDescending(c => c.Score).ToList();
    }
}
