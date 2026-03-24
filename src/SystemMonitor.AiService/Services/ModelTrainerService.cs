using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.FastTree;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.AiService.Services;

public class ModelTrainerService
{
    private readonly MLContext _ml;
    private readonly BlobContainerClient _blobContainer;
    private readonly ModelStore _store;
    private readonly ILogger<ModelTrainerService> _logger;

    private const string ModelBlobName = "anomaly-model.zip";

    public ModelTrainerService(
        BlobContainerClient blobContainer,
        ModelStore store,
        ILogger<ModelTrainerService> logger)
    {
        _ml = new MLContext(seed: 42);
        _blobContainer = blobContainer;
        _store = store;
        _logger = logger;
    }

    public async Task TrainAsync(
        IReadOnlyList<TrainingDataRecord> records,
        CancellationToken ct)
    {
        _logger.LogInformation("Starting model training with {Count} records", records.Count);

        var mlData = records.Select(r => new TrainingInput
        {
            CpuPercent = (float)r.CpuPercent,
            MemoryPercent = (float)r.MemoryPercent,
            DiskReadMbps = (float)r.DiskReadMbps,
            DiskWriteMbps = (float)r.DiskWriteMbps,
            NetworkInMbps = (float)r.NetworkInMbps,
            NetworkOutMbps = (float)r.NetworkOutMbps,
            RequestsPerSecond = (float)r.RequestsPerSecond,
            ErrorRate = (float)r.ErrorRate,
            P99LatencyMs = (float)r.P99LatencyMs,
            Label = r.IsAnomaly
        }).ToList();

        var view = _ml.Data.LoadFromEnumerable(mlData);
        var split = _ml.Data.TrainTestSplit(view, testFraction: 0.2);

        var pipeline = _ml.Transforms
            .Concatenate("Features",
                nameof(TrainingInput.CpuPercent),
                nameof(TrainingInput.MemoryPercent),
                nameof(TrainingInput.DiskReadMbps),
                nameof(TrainingInput.DiskWriteMbps),
                nameof(TrainingInput.NetworkInMbps),
                nameof(TrainingInput.NetworkOutMbps),
                nameof(TrainingInput.RequestsPerSecond),
                nameof(TrainingInput.ErrorRate),
                nameof(TrainingInput.P99LatencyMs))
            .Append(_ml.Transforms.NormalizeMinMax("Features"))
            .Append(_ml.BinaryClassification.Trainers.FastTree(
                new FastTreeBinaryTrainer.Options
                {
                    NumberOfLeaves = 20,
                    NumberOfTrees = 100,
                    MinimumExampleCountPerLeaf = 5,
                    LearningRate = 0.1f
                }));

        var model = pipeline.Fit(split.TrainSet);
        var metrics = _ml.BinaryClassification.Evaluate(model.Transform(split.TestSet));

        _logger.LogInformation(
            "Model trained — AUC={Auc:F4}  F1={F1:F4}  Accuracy={Acc:F4}",
            metrics.AreaUnderRocCurve, metrics.F1Score, metrics.Accuracy);

        using var stream = new MemoryStream();
        _ml.Model.Save(model, view.Schema, stream);
        stream.Position = 0;

        try
        {
            var blobClient = _blobContainer.GetBlobClient(ModelBlobName);
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken: ct);
            _logger.LogInformation("Model saved to blob '{Blob}'", ModelBlobName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not persist model to blob — model is active in-memory only");
        }

        stream.Position = 0;
        _store.LoadFromStream(stream);
        _logger.LogInformation("Model hot-reloaded into ModelStore");
    }

    public async Task LoadLatestModelAsync(CancellationToken ct)
    {
        try
        {
            var blob = _blobContainer.GetBlobClient(ModelBlobName);
            if (!await blob.ExistsAsync(ct))
            {
                _logger.LogInformation(
                    "No trained model found in blob storage — using statistical fallback");
                return;
            }

            var download = await blob.DownloadContentAsync(ct);
            using var stream = download.Value.Content.ToStream();
            _store.LoadFromStream(stream);
            _logger.LogInformation("Loaded anomaly detection model from blob storage");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Could not load model from blob — using statistical fallback (z-score + SR-CNN)");
        }
    }

    private class TrainingInput
    {
        public float CpuPercent { get; set; }
        public float MemoryPercent { get; set; }
        public float DiskReadMbps { get; set; }
        public float DiskWriteMbps { get; set; }
        public float NetworkInMbps { get; set; }
        public float NetworkOutMbps { get; set; }
        public float RequestsPerSecond { get; set; }
        public float ErrorRate { get; set; }
        public float P99LatencyMs { get; set; }
        public bool Label { get; set; }
    }
}

public class ModelStore
{
    private readonly MLContext _ml = new(seed: 42);
    private ITransformer? _model;
    private DataViewSchema? _schema;
    private readonly ReaderWriterLockSlim _lock = new();

    public bool HasModel
    {
        get
        {
            _lock.EnterReadLock();
            try { return _model is not null; }
            finally { _lock.ExitReadLock(); }
        }
    }

    public void LoadFromStream(Stream stream)
    {
        var loaded = _ml.Model.Load(stream, out var schema);

        _lock.EnterWriteLock();
        try { _model = loaded; _schema = schema; }
        finally { _lock.ExitWriteLock(); }
    }

    public (ITransformer model, DataViewSchema schema)? GetModel()
    {
        _lock.EnterReadLock();
        try
        {
            if (_model is null) return null;
            return (_model, _schema!);
        }
        finally { _lock.ExitReadLock(); }
    }
}