namespace SystemMonitor.Shared.Models;

public record CanonicalAttribute
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string DataType { get; init; } = "double";
    public bool IsRequired { get; init; }
    public double? MinValue { get; init; }
    public double? MaxValue { get; init; }
    public List<string> KnownAliases { get; init; } = new();
    public float[]? EmbeddingVector { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public record AttributeResolutionResult
{
    public string RawName { get; init; } = string.Empty;
    public CanonicalAttribute? Resolved { get; init; }
    public double Confidence { get; init; }
    public ResolutionMethod Method { get; init; }
    public bool IsConfirmed { get; init; }
    public bool IsResolved => Resolved is not null;
}

public record UnknownAttribute
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string RawName { get; init; } = string.Empty;
    public string SourceAgentId { get; init; } = string.Empty;
    public string SourceVersion { get; init; } = string.Empty;
    public DateTimeOffset SeenAt { get; init; } = DateTimeOffset.UtcNow;
    public int OccurrenceCount { get; set; } = 1;
    public string? SuggestedCanonicalId { get; init; }
    public double SuggestionConfidence { get; init; }
    public ResolutionMethod SuggestionMethod { get; init; }
    public List<ResolutionCandidate> Candidates { get; init; } = new();
    public UnknownAttributeStatus Status { get; set; } = UnknownAttributeStatus.Pending;
    public string? ConfirmedCanonicalId { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string? ReviewNote { get; set; }
}

public record ResolutionCandidate
{
    public string CanonicalId { get; init; } = string.Empty;
    public string CanonicalName { get; init; } = string.Empty;
    public double Score { get; init; }
    public ResolutionMethod Method { get; init; }
}

public record SchemaSnapshot
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string AgentId { get; init; } = string.Empty;
    public string SourceVersion { get; init; } = string.Empty;
    public DateTimeOffset CapturedAt { get; init; } = DateTimeOffset.UtcNow;
    public HashSet<string> AttributeNames { get; init; } = new();
    public Dictionary<string, string> ResolvedMappings { get; init; } = new();
}

public record NormalisedDataBlock
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string AgentId { get; init; } = string.Empty;
    public string HostName { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public Dictionary<string, object> Fields { get; init; } = new();
    public Dictionary<string, object> UnresolvedFields { get; init; } = new();
    public Dictionary<string, AttributeResolutionResult> ResolutionAudit { get; init; } = new();
    public bool HasUnresolved => UnresolvedFields.Count > 0;
    public double ResolutionRate =>
        Fields.Count == 0 && UnresolvedFields.Count == 0
            ? 1.0
            : (double)Fields.Count / (Fields.Count + UnresolvedFields.Count);
}
public enum ResolutionMethod
{
    ExactMatch,
    AliasMatch,
    FuzzyMatch,
    SemanticMatch,
    SchemaDiff,
    ManualConfirm,
    Unresolved
}

public enum UnknownAttributeStatus
{
    Pending,
    Confirmed,
    Rejected,
    AutoResolved
}
