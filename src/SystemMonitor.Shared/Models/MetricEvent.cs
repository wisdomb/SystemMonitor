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
    public bool IsAnomaly { get; init; }   // label for supervised training
}

public record IngestionBatch<T>
{
    public string BatchId { get; init; } = Guid.NewGuid().ToString();
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
