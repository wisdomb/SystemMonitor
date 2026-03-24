using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using SystemMonitor.Shared.Models;
using SharedLogLevel = SystemMonitor.Shared.Models.LogLevel;

namespace SystemMonitor.Agent.Collectors;

public class LogCollector
{
    private readonly IConfiguration _cfg;
    private readonly ConcurrentQueue<LogEvent> _buffer = new();
    private readonly string _agentId;

    private string _syslogPath = "/var/log/syslog";
    private long _syslogOffset;

    public LogCollector(IConfiguration cfg)
    {
        _cfg = cfg;
        _agentId = cfg["Agent:Id"]!;

        if (File.Exists(_syslogPath))
            _syslogOffset = new FileInfo(_syslogPath).Length;
    }

    public void Emit(
        SharedLogLevel level,
        string message,
        string serviceName = "agent",
        string? stackTrace = null,
        Dictionary<string, string>? properties = null)
    {
        _buffer.Enqueue(new LogEvent
        {
            AgentId = _agentId,
            HostName = Environment.MachineName,
            ServiceName = serviceName,
            Level = level,
            Message = message,
            StackTrace = stackTrace,
            Properties = properties ?? new()
        });
    }

    public IReadOnlyList<LogEvent> Drain()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            TailSyslog();

        var results = new List<LogEvent>();

        while (_buffer.TryDequeue(out var evt))
            results.Add(evt);
        return results;
    }

    private void TailSyslog()
    {
        if (!File.Exists(_syslogPath)) return;
        try
        {
            using var stream = new FileStream(
                _syslogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            stream.Seek(_syslogOffset, SeekOrigin.Begin);

            using var reader = new StreamReader(stream);
            string? line;
            int count = 0;

            while ((line = reader.ReadLine()) is not null && count < 500)
            {
                _buffer.Enqueue(new LogEvent
                {
                    AgentId = _agentId,
                    HostName = Environment.MachineName,
                    ServiceName = "syslog",
                    Level = ClassifySyslogLine(line),
                    Message = line,
                    Properties = new Dictionary<string, string> { ["source"] = "syslog" }
                });
                count++;
            }

            _syslogOffset = stream.Position;
        }
        catch { }
    }

    private static SharedLogLevel ClassifySyslogLine(string line)
    {
        if (line.Contains("ERROR") || line.Contains("CRIT") || line.Contains("emerg"))
            return SharedLogLevel.Error;
        if (line.Contains("WARN") || line.Contains("WARNING"))
            return SharedLogLevel.Warning;
        return SharedLogLevel.Information;
    }
}
