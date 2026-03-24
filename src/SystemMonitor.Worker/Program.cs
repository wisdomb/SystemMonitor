using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SystemMonitor.Worker.Workers;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        var config = ctx.Configuration;
        bool useInMemory = config.GetValue<bool>("ServiceBus:UseInMemory");

        if (!useInMemory)
            services.AddSingleton(_ =>
                new ServiceBusClient(config["ServiceBus:ConnectionString"]!));

        services.AddSingleton(_ =>
        {
            var endpoint = config["CosmosDb:Endpoint"]!;
            var key = config["CosmosDb:Key"]!;

            var opts = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase }
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

        services.AddHttpClient("AiService", c =>
            c.BaseAddress = new Uri(config["AiService:BaseUrl"]!));

        if (useInMemory)
            services.AddHostedService<InMemoryProcessorWorker>();
        else
        {
            services.AddHostedService<MetricProcessorWorker>();
            services.AddHostedService<LogProcessorWorker>();
            services.AddHostedService<TrainingDataWorker>();
        }

        services.AddHostedService<HealthScoreWorker>();
    })
    .UseSerilog((hostCtx, logConfig) => logConfig
        .ReadFrom.Configuration(hostCtx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console())
    .Build();

await host.RunAsync();