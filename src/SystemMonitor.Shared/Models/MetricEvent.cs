namespace SystemMonitor.Shared.Models;

public record MetricEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string AgentId { get; init; } = string.Empty;
    public string HostName { get; init; } = string.Empty;
    public string Environment { get; init; } = "production";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public MetricType Type { get; init; }
    public Dictionary<string, double> Values { get; init; } = new();
    public Dictionary<string, string> Tags { get; init; } = new();
}

public record LogEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string AgentId { get; init; } = string.Empty;
    public string HostName { get; init; } = string.Empty;
    public string ServiceName { get; init; } = string.Empty;
    public string Environment { get; init; } = "production";
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? StackTrace { get; init; }
    public Dictionary<string, string> Properties { get; init; } = new();
}

public record AnomalyResult
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SourceEventId { get; init; }
    public string AgentId { get; init; } = string.Empty;
    public string HostName { get; init; } = string.Empty;
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsAnomaly { get; init; }
    public double Confidence { get; init; }
    public AnomalyType Type { get; init; }
    public AnomalySeverity Severity { get; init; }
    public string Description { get; init; } = string.Empty;
    public Dictionary<string, double> AffectedMetrics { get; init; } = new();
}

public record TrainingDataRecord
{
    public DateTimeOffset Timestamp { get; init; }
    public double CpuPercent { get; init; }
    public double MemoryPercent { get; init; }
    public double DiskReadMbps { get; init; }
    public double DiskWriteMbps { get; init; }
    public double NetworkInMbps { get; init; }
    public double NetworkOutMbps { get; init; }
    public double RequestsPerSecond { get; init; }
    public double ErrorRate { get; init; }
    public double P99LatencyMs { get; init; }
    public bool IsAnomaly { get; init; }
    public string? TenantId { get; set; }
}

public record IngestionBatch<T>
{
    public string BatchId { get; init; } = Guid.NewGuid().ToString();
    public string? TenantId { get; init; }
    public DateTimeOffset ReceivedAt { get; init; } = DateTimeOffset.UtcNow;
    public IReadOnlyList<T> Events { get; init; } = Array.Empty<T>();
}

public enum MetricType
{
    System,
    Application,
    Network,
    Database,
    Custom
}

public enum AnomalyType
{
    Spike,
    Drop,
    ChangePoint,
    Pattern,
    Correlation
}

public enum AnomalySeverity
{
    Low,
    Medium,
    High,
    Critical
}

public enum LogLevel
{
    Trace,
    Debug,
    Information,
    Warning,
    Error,
    Critical
}

public record Tenant
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Plan { get; init; } = "standard";
    public string ApiKey { get; init; } = GenerateApiKey();
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    public string ContactEmail { get; init; } = string.Empty;
    public string ContactName { get; init; } = string.Empty;

    public double UptimeTargetPercent { get; init; } = 99.9;
    public int MaxP99LatencyMs { get; init; } = 500;
    public double MaxErrorRate { get; init; } = 0.01;

    public string PartitionKey => Id;

    private static string GenerateApiKey()
        => "sk_" + Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "").Replace("/", "").Replace("=", "")
            .Substring(0, 24);
}

public record AgentRegistration
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string TenantId { get; init; } = string.Empty;
    public string AgentId { get; init; } = string.Empty;
    public string HostName { get; init; } = string.Empty;
    public string Environment { get; init; } = "production";
    public Dictionary<string, string> Tags { get; init; } = new();
    public DateTimeOffset FirstSeenAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public string PartitionKey => TenantId;
}

public record AppUser
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Email { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Role { get; init; } = "Analyst";
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLogin { get; set; }

    public List<string> AssignedTenantIds { get; init; } = new();

    public string PartitionKey => Id;
}

public record RefreshToken
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string UserId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool IsRevoked { get; set; } = false;
    public string PartitionKey => UserId;
}

public record SchemaResolutionEvent
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string AgentId { get; init; } = string.Empty;
    public string HostName { get; init; } = string.Empty;
    public string RawAttribute { get; init; } = string.Empty;
    public string? ResolvedAttribute { get; init; }
    public double Confidence { get; init; }
    public string ResolutionTier { get; init; } = string.Empty;
    public bool WasResolved { get; init; }
    public DateTimeOffset DetectedAt { get; init; } = DateTimeOffset.UtcNow;
    public string PartitionKey => AgentId;
}