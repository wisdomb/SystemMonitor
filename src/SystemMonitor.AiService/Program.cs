using Azure.Storage.Blobs;
using Serilog;
using SystemMonitor.AiService.Services;

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();
builder.Host.UseSerilog();

builder.Services.AddSingleton(sp =>
{
    var connStr = builder.Configuration["AzureStorage:ConnectionString"]!;
    var containerName = builder.Configuration["AzureStorage:ModelContainer"] ?? "ai-models";
    var client = new BlobContainerClient(connStr, containerName);

    try { client.CreateIfNotExists(); }
    catch (Exception ex)
    {
        var logger = sp.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Could not create blob container — model persistence unavailable");
    }

    return client;
});

builder.Services.AddSingleton<ModelStore>();
builder.Services.AddSingleton<ModelTrainerService>();
builder.Services.AddSingleton<AnomalyDetectionService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "SystemMonitor AI Service", Version = "v1" }));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseSerilogRequestLogging();
app.MapControllers();

_ = Task.Run(async () =>
{
    await Task.Delay(1000);
    var trainer = app.Services.GetRequiredService<ModelTrainerService>();
    await trainer.LoadLatestModelAsync(CancellationToken.None);
});

app.Run();