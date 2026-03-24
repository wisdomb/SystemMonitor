using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.TimeSeries;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.AiService.Services;

public class AnomalyDetectionService
{
    private readonly MLContext _ml;
    private readonly ModelStore _store;
    private readonly ILogger<AnomalyDetectionService> _logger;

    public AnomalyDetectionService(
        ModelStore store,
        ILogger<AnomalyDetectionService> logger)
    {
        _ml = new MLContext(seed: 42);
        _store = store;
        _logger = logger;
    }

    public List<AnomalyResult> AnalyzeMetrics(
        IReadOnlyList<MetricEvent> events,
        double threshold = 0.75)
    {
        if (events.Count < 2) return new();

        var results = new List<AnomalyResult>();
        var seriesGroups = events
            .SelectMany(e => e.Values.Select(kv =>
                (evt: e, key: kv.Key, value: kv.Value)))
            .GroupBy(x => (x.evt.AgentId, x.key))
            .Select(g => (
                agentId: g.Key.AgentId,
                metricKey: g.Key.key,
                items: g.ToList()
            ));

        foreach (var (agentId, metricKey, items) in seriesGroups)
        {
            var values = items.Select(x => (float)x.value).ToArray();

            if (values.Length >= 12)
            {
                var srResults = RunSrCnn(values);
                for (int i = 0; i < srResults.Count && i < items.Count; i++)
                {
                    var (isAnomaly, score) = srResults[i];
                    if (isAnomaly && score >= threshold)
                        results.Add(BuildResult(
                            items[i].evt, metricKey, score, AnomalyType.Pattern));
                }
            }

            var spikes = DetectSpikes(values);
            for (int i = 0; i < spikes.Count && i < items.Count; i++)
            {
                var (isSpike, confidence) = spikes[i];
                if (isSpike && confidence >= threshold)
                    results.Add(BuildResult(
                        items[i].evt, metricKey, confidence, AnomalyType.Spike));
            }
        }

        return results
            .GroupBy(r => r.SourceEventId)
            .Select(g => g.OrderByDescending(r => r.Confidence).First())
            .ToList();
    }

    private List<(bool isAnomaly, double score)> RunSrCnn(float[] values)
    {
        try
        {
            var data = values
                .Select((v, i) => new TimeSeriesInput { Value = v, Index = i })
                .ToList();

            var view = _ml.Data.LoadFromEnumerable(data);
            var output = _ml.AnomalyDetection.DetectEntireAnomalyBySrCnn(
                input: view,
                outputColumnName: "Prediction",
                inputColumnName: "Value",
                threshold: 0.35,
                batchSize: 512,
                sensitivity: 60,
                detectMode: SrCnnDetectMode.AnomalyAndMargin);

            return _ml.Data
                .CreateEnumerable<SrCnnOutput>(output, reuseRowObject: false)
                .Select(p => (p.Prediction[0] > 0, (double)p.Prediction[3]))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SR-CNN failed — skipping");
            return new();
        }
    }

    private static List<(bool isSpike, double confidence)> DetectSpikes(float[] values)
    {
        if (values.Length < 3)
            return Enumerable.Repeat((false, 0.0), values.Length).ToList();

        double mean = values.Average();
        double stdDev = Math.Sqrt(values.Average(v => Math.Pow(v - mean, 2)));

        if (stdDev < 1e-9)
            return Enumerable.Repeat((false, 0.0), values.Length).ToList();

        return values.Select(v =>
        {
            double z = Math.Abs((v - mean) / stdDev);
            bool isSpike = z > 3.0;
            double confidence = Math.Min(1.0, z / 6.0);
            return (isSpike, confidence);
        }).ToList();
    }

    private static AnomalyResult BuildResult(
        MetricEvent source, string metricKey,
        double confidence, AnomalyType type)
    {
        var severity = confidence switch
        {
            >= 0.95 => AnomalySeverity.Critical,
            >= 0.85 => AnomalySeverity.High,
            >= 0.75 => AnomalySeverity.Medium,
            _ => AnomalySeverity.Low
        };

        return new AnomalyResult
        {
            SourceEventId = source.Id,
            AgentId = source.AgentId,
            HostName = source.HostName,
            IsAnomaly = true,
            Confidence = confidence,
            Type = type,
            Severity = severity,
            Description = $"Anomaly in '{metricKey}' — " +
                            $"value={source.Values.GetValueOrDefault(metricKey):F2}",
            AffectedMetrics = source.Values
        };
    }

    private class TimeSeriesInput
    {
        public float Value { get; set; }
        public int Index { get; set; }
    }

    private class SrCnnOutput
    {
        [Microsoft.ML.Data.VectorType(5)]
        public float[] Prediction { get; set; } = Array.Empty<float>();
    }
}