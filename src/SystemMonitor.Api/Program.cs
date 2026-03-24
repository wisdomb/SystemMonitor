using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using FluentValidation;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using SystemMonitor.Api.Hubs;
using SystemMonitor.Api.Middleware;
using SystemMonitor.Api.Services;

Console.WriteLine("[Startup] Building application...");

var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateLogger();

builder.Host.UseSerilog();

bool useInMemory = builder.Configuration.GetValue<bool>("ServiceBus:UseInMemory");

if (!useInMemory)
    builder.Services.AddSingleton(_ =>
        new ServiceBusClient(builder.Configuration["ServiceBus:ConnectionString"]!));

if (useInMemory)
    builder.Services.AddScoped<IIngestionService, InMemoryIngestionService>();
else
    builder.Services.AddScoped<IIngestionService, IngestionService>();

builder.Services.AddSingleton(_ =>
{
    var endpoint = builder.Configuration["CosmosDb:Endpoint"]!;
    var key = builder.Configuration["CosmosDb:Key"]!;

    var opts = new CosmosClientOptions
    {
        SerializerOptions = new CosmosSerializationOptions
        { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
        RequestTimeout = TimeSpan.FromSeconds(10)
    };

    if (endpoint.Contains("localhost") || endpoint.Contains("127.0.0.1"))
    {
        opts.HttpClientFactory = () => new HttpClient(
            new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            });
        opts.ConnectionMode = ConnectionMode.Gateway;
    }

    return new CosmosClient(endpoint, key, opts);
});

builder.Services.AddHttpClient<IAiServiceClient, AiServiceClient>(c =>
{
    c.BaseAddress = new Uri(builder.Configuration["AiService:BaseUrl"]!);
    c.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient("AzureOpenAI", c =>
    c.Timeout = TimeSpan.FromSeconds(15));
builder.Services.AddSingleton<TelemetryCache>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<ICosmosRepository, CosmosRepository>();
builder.Services.AddSingleton<ISchemaRegistryRepository, SchemaRegistryRepository>();
builder.Services.AddSingleton<ISemanticSimilarityService, SemanticSimilarityService>();
builder.Services.AddScoped<IAttributeNormalizationService, AttributeNormalizationService>();

var signalRBuilder = builder.Services.AddSignalR()
    .AddJsonProtocol(opts =>
    {
        opts.PayloadSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
        opts.PayloadSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });
var signalRConn = builder.Configuration["AzureSignalR:ConnectionString"];
if (!string.IsNullOrWhiteSpace(signalRConn) && signalRConn.StartsWith("Endpoint="))
    signalRBuilder.AddAzureSignalR(signalRConn);

// ── CORS ──────────────────────────────────────────────────────────────────────
builder.Services.AddCors(o => o.AddPolicy("Dashboard", p =>
    p.WithOrigins(
        builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
        ?? new[] { "http://localhost:5173" })
     .AllowAnyMethod()
     .AllowAnyHeader()
     .AllowCredentials()));

builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
        o.JsonSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    c.SwaggerDoc("v1", new() { Title = "SystemMonitor API", Version = "v1" }));

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", o =>
    {
        o.Authority = builder.Configuration["Auth:Authority"];
        o.Audience = builder.Configuration["Auth:Audience"];
        o.RequireHttpsMetadata = false;
    });
builder.Services.AddAuthorization();

Console.WriteLine("[Startup] Registrations complete, building app...");
var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseMiddleware<RequestTimingMiddleware>();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("Dashboard");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MonitoringHub>("/hubs/monitoring");

_ = Task.Run(async () =>
{
    await Task.Delay(2000);
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Starting Cosmos bootstrap...");
        using var scope = app.Services.CreateScope();
        var cosmos = scope.ServiceProvider.GetRequiredService<CosmosClient>();
        var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var db = await cosmos.CreateDatabaseIfNotExistsAsync(cfg["CosmosDb:Database"]);
        await db.Database.CreateContainerIfNotExistsAsync("metrics", "/agentId");
        await db.Database.CreateContainerIfNotExistsAsync("logs", "/agentId");
        await db.Database.CreateContainerIfNotExistsAsync("anomalies", "/agentId");
        await db.Database.CreateContainerIfNotExistsAsync("trainingData", "/id");
        await db.Database.CreateContainerIfNotExistsAsync("canonicalAttributes", "/id");
        await db.Database.CreateContainerIfNotExistsAsync("unknownAttributes", "/id");
        await db.Database.CreateContainerIfNotExistsAsync("schemaSnapshots", "/agentId");
        await db.Database.CreateContainerIfNotExistsAsync("tenants", "/id");
        await db.Database.CreateContainerIfNotExistsAsync("agentRegistrations", "/tenantId");

        var registry = scope.ServiceProvider.GetRequiredService<ISchemaRegistryRepository>();
        var existing = await registry.GetAllCanonicalAttributesAsync(CancellationToken.None);
        if (existing.Count == 0)
        {
            logger.LogInformation("Seeding canonical attributes...");
            foreach (var attr in CanonicalAttributeSeeder.GetSeedAttributes())
                await registry.UpsertCanonicalAttributeAsync(attr, CancellationToken.None);
            logger.LogInformation("Seeded {Count} canonical attributes",
                CanonicalAttributeSeeder.GetSeedAttributes().Count);
        }

        logger.LogInformation("Cosmos bootstrap complete");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Cosmos bootstrap failed — " +
            "is the emulator running? (docker compose up cosmos-emulator)");
    }
});

Console.WriteLine("[Startup] Starting web server...");
app.Run();