using System.Runtime.InteropServices;
using System.Diagnostics;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Agent.Collectors;

public class MetricCollector
{
    private readonly IConfiguration _cfg;
    private readonly ILogger<MetricCollector> _logger;

    private long _prevNetBytesIn;
    private long _prevNetBytesOut;
    private DateTime _prevNetTime = DateTime.UtcNow;

    public MetricCollector(IConfiguration cfg, ILogger<MetricCollector> logger)
    {
        _cfg = cfg;
        _logger = logger;
    }

    public MetricEvent Collect()
    {
        var values = new Dictionary<string, double>();

        CollectCpu(values);
        CollectMemory(values);
        CollectDisk(values);
        CollectNetwork(values);
        CollectProcess(values);

        return new MetricEvent
        {
            AgentId = _cfg["Agent:Id"]!,
            HostName = Environment.MachineName,
            Environment = _cfg["Agent:Environment"] ?? "production",
            Type = MetricType.System,
            Values = values,
            Tags = new Dictionary<string, string>
            {
                ["os"] = RuntimeInformation.OSDescription,
                ["runtime"] = RuntimeInformation.FrameworkDescription
            }
        };
    }

    // ── CPU ───────────────────────────────────────────────────────────────────

    private void CollectCpu(Dictionary<string, double> values)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var lines = File.ReadAllLines("/proc/stat");
                var cpu = lines[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                long user = long.Parse(cpu[1]);
                long nice = long.Parse(cpu[2]);
                long sys = long.Parse(cpu[3]);
                long idle = long.Parse(cpu[4]);
                long total = user + nice + sys + idle;
                long busy = total - idle;
                values["cpu_percent"] = total > 0 ? (double)busy / total * 100 : 0;
            }
            else
            {
                // Fallback: process CPU heuristic
                using var proc = Process.GetCurrentProcess();
                values["cpu_percent"] = proc.TotalProcessorTime.TotalSeconds /
                    (Environment.ProcessorCount * (DateTime.UtcNow - proc.StartTime).TotalSeconds) * 100;
            }

            values["cpu_count"] = Environment.ProcessorCount;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "CPU collection failed");
            values["cpu_percent"] = -1;
        }
    }

    private void CollectMemory(Dictionary<string, double> values)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var lines = File.ReadAllLines("/proc/meminfo")
                    .ToDictionary(
                        l => l.Split(':')[0].Trim(),
                        l => ParseKb(l.Split(':')[1]));

                double total = lines["MemTotal"];
                double available = lines.GetValueOrDefault("MemAvailable", lines.GetValueOrDefault("MemFree", 0));
                double used = total - available;

                values["memory_total_mb"] = total / 1024;
                values["memory_used_mb"] = used / 1024;
                values["memory_percent"] = total > 0 ? used / total * 100 : 0;
            }
            else
            {
                var gc = GC.GetGCMemoryInfo();
                values["memory_total_mb"] = gc.TotalAvailableMemoryBytes / 1024.0 / 1024;
                values["memory_used_mb"] = GC.GetTotalMemory(false) / 1024.0 / 1024;
                values["memory_percent"] = gc.TotalAvailableMemoryBytes > 0
                    ? (double)GC.GetTotalMemory(false) / gc.TotalAvailableMemoryBytes * 100
                    : 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Memory collection failed");
        }
    }

    private void CollectDisk(Dictionary<string, double> values)
    {
        try
        {
            var root = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady);
            if (root is not null)
            {
                double total = root.TotalSize;
                double free = root.AvailableFreeSpace;
                values["disk_total_gb"] = total / 1e9;
                values["disk_free_gb"] = free / 1e9;
                values["disk_percent"] = total > 0 ? (total - free) / total * 100 : 0;
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var stats = File.ReadAllLines("/proc/diskstats");
                long readSectors = 0, writeSectors = 0;
                foreach (var line in stats)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 14 && parts[2].StartsWith("sd"))
                    {
                        readSectors += long.Parse(parts[5]);
                        writeSectors += long.Parse(parts[9]);
                    }
                }
                values["disk_read_mbps"] = readSectors * 512.0 / 1e6;
                values["disk_write_mbps"] = writeSectors * 512.0 / 1e6;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Disk collection failed");
        }
    }

    private void CollectNetwork(Dictionary<string, double> values)
    {
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                var lines = File.ReadAllLines("/proc/net/dev").Skip(2);
                long bytesIn = 0, bytesOut = 0;

                foreach (var line in lines)
                {
                    var parts = line.Split(':', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;
                    var cols = parts[1].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (cols.Length >= 9)
                    {
                        bytesIn += long.Parse(cols[0]);
                        bytesOut += long.Parse(cols[8]);
                    }
                }

                var now = DateTime.UtcNow;
                var elapsed = (now - _prevNetTime).TotalSeconds;
                if (elapsed > 0 && _prevNetBytesIn > 0)
                {
                    values["network_in_mbps"] = (bytesIn - _prevNetBytesIn) / elapsed / 1e6;
                    values["network_out_mbps"] = (bytesOut - _prevNetBytesOut) / elapsed / 1e6;
                }

                _prevNetBytesIn = bytesIn;
                _prevNetBytesOut = bytesOut;
                _prevNetTime = now;
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Network collection failed");
        }
    }

    private static void CollectProcess(Dictionary<string, double> values)
    {
        using var proc = Process.GetCurrentProcess();
        values["process_threads"] = proc.Threads.Count;
        values["process_handles"] = proc.HandleCount;
        values["gc_gen0_collections"] = GC.CollectionCount(0);
        values["gc_gen1_collections"] = GC.CollectionCount(1);
        values["gc_gen2_collections"] = GC.CollectionCount(2);
    }

    private static double ParseKb(string raw)
        => double.TryParse(raw.Replace("kB", "").Trim(), out var v) ? v : 0;
}
