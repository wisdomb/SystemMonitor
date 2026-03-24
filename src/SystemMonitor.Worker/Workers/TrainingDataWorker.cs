using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Text.Json;
using SystemMonitor.Shared.Models;

namespace SystemMonitor.Worker.Workers;

public class TrainingDataWorker : BackgroundService
{
    private readonly ServiceBusClient _sbClient;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _cfg;
    private readonly ILogger<TrainingDataWorker> _logger;

    private readonly List<TrainingDataRecord> _buffer = new();
    private const int MinRecordsToTrain = 200;

    private static readonly JsonSerializerOptions JsonOpts = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public TrainingDataWorker(
        ServiceBusClient sbClient,
        IHttpClientFactory httpFactory,
        IConfiguration cfg,
        ILogger<TrainingDataWorker> logger)
    {
        _sbClient = sbClient;
        _httpFactory = httpFactory;
        _cfg = cfg;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var processor = _sbClient.CreateProcessor(
            "training-queue",
            new ServiceBusProcessorOptions
            {
                MaxConcurrentCalls = 1,
                AutoCompleteMessages = false
            });

        processor.ProcessMessageAsync += async args =>
        {
            try
            {
                var records = JsonSerializer.Deserialize<List<TrainingDataRecord>>(
                    args.Message.Body.ToArray(), JsonOpts)!;

                lock (_buffer) _buffer.AddRange(records);

                _logger.LogInformation(
                    "Buffered {New} training records (total={Total})",
                    records.Count, _buffer.Count);

                if (_buffer.Count >= MinRecordsToTrain)
                    await TriggerRetrainingAsync(stoppingToken);

                await args.CompleteMessageAsync(args.Message, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing training batch");
                await args.AbandonMessageAsync(args.Message,
                    cancellationToken: stoppingToken);
            }
        };

        processor.ProcessErrorAsync += args =>
        {
            _logger.LogError(args.Exception, "Training queue Service Bus error");
            return Task.CompletedTask;
        };

        await processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        await processor.StopProcessingAsync();
    }

    private async Task TriggerRetrainingAsync(CancellationToken ct)
    {
        List<TrainingDataRecord> snapshot;
        lock (_buffer)
        {
            snapshot = new List<TrainingDataRecord>(_buffer);
            _buffer.Clear();
        }

        _logger.LogInformation(
            "Triggering AI retraining with {Count} records", snapshot.Count);

        try
        {
            var http = _httpFactory.CreateClient("AiService");
            var content = new StringContent(
                JsonSerializer.Serialize(snapshot, JsonOpts),
                System.Text.Encoding.UTF8, "application/json");

            var response = await http.PostAsync("/train", content, ct);
            if (response.IsSuccessStatusCode)
                _logger.LogInformation("AI retraining completed");
            else
                _logger.LogWarning("AI service returned {Status}", response.StatusCode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger AI retraining");
            lock (_buffer) _buffer.AddRange(snapshot);
        }
    }
}