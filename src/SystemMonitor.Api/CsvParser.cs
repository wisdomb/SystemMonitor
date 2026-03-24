using System.Globalization;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Api;

public static class CsvParser
{
    public static List<TrainingDataRecord> ParseTrainingData(string csv)
    {
        var records = new List<TrainingDataRecord>();
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        if (lines.Length < 2) return records;

        var headers = lines[0].Trim().Split(',')
            .Select((h, i) => (h.Trim().ToLowerInvariant(), i))
            .ToDictionary(x => x.Item1, x => x.i);

        int Col(string name) => headers.TryGetValue(name, out var i) ? i : -1;

        int idxTimestamp = Col("timestamp");
        int idxCpu = Col("cpu_percent");
        int idxMem = Col("memory_percent");
        int idxDiskR = Col("disk_read_mbps");
        int idxDiskW = Col("disk_write_mbps");
        int idxNetIn = Col("network_in_mbps");
        int idxNetOut = Col("network_out_mbps");
        int idxRps = Col("requests_per_second");
        int idxErr = Col("error_rate");
        int idxP99 = Col("p99_latency_ms");
        int idxLabel = Col("is_anomaly");

        for (int i = 1; i < lines.Length; i++)
        {
            var parts = lines[i].Trim().Split(',');
            if (parts.Length < 2) continue;

            try
            {
                records.Add(new TrainingDataRecord
                {
                    Timestamp = idxTimestamp >= 0
                        ? DateTimeOffset.Parse(parts[idxTimestamp].Trim(), CultureInfo.InvariantCulture)
                        : DateTimeOffset.UtcNow,
                    CpuPercent = ParseDouble(parts, idxCpu),
                    MemoryPercent = ParseDouble(parts, idxMem),
                    DiskReadMbps = ParseDouble(parts, idxDiskR),
                    DiskWriteMbps = ParseDouble(parts, idxDiskW),
                    NetworkInMbps = ParseDouble(parts, idxNetIn),
                    NetworkOutMbps = ParseDouble(parts, idxNetOut),
                    RequestsPerSecond = ParseDouble(parts, idxRps),
                    ErrorRate = ParseDouble(parts, idxErr),
                    P99LatencyMs = ParseDouble(parts, idxP99),
                    IsAnomaly = idxLabel >= 0
                        && ParseBool(parts[idxLabel].Trim())
                });
            }
            catch { }
        }

        return records;
    }

    private static double ParseDouble(string[] parts, int idx)
    {
        if (idx < 0 || idx >= parts.Length) return 0;
        return double.TryParse(parts[idx].Trim(), NumberStyles.Any,
            CultureInfo.InvariantCulture, out var v) ? v : 0;
    }

    private static bool ParseBool(string raw)
        => raw is "1" or "true" or "True" or "TRUE" or "yes";
}
