using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SystemMonitor.Agent;
using SystemMonitor.Agent.Generators;
using Microsoft.Extensions.Logging;
using SystemMonitor.Agent.Senders;

IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        var cfg = ctx.Configuration;

        services.AddHttpClient("ApiGateway", client =>
        {
            client.BaseAddress = new Uri(cfg["Api:BaseUrl"]!);
            client.DefaultRequestHeaders.Add("X-Agent-Key", cfg["Api:AgentKey"]);
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddTransient<MockMetricGenerator>();
        services.AddTransient<MockLogGenerator>();
        var agentSection = cfg.GetSection("Agents");
        var agents = agentSection.Get<List<AgentConfig>>() ?? new List<AgentConfig>
        {
            new AgentConfig
            {
                Id       = cfg["Agent:Id"]       ?? "agent-001",
                HostName = cfg["Agent:HostName"]  ?? "localhost",
            }
        };

        Console.WriteLine($"[Startup] Loaded {agents.Count} agent(s) from config: {string.Join(", ", agents.Select(a => a.Id))}");

        foreach (var agentCfg in agents)
        {
            var capturedCfg = agentCfg;
            services.AddHostedService(sp => new AgentWorker(
                new MockMetricGenerator(),
                new MockLogGenerator(),
                new TelemetrySender(
                    sp.GetRequiredService<IHttpClientFactory>(),
                    cfg,
                    sp.GetRequiredService<ILogger<TelemetrySender>>()),
                cfg,
                capturedCfg,
                sp.GetRequiredService<ILogger<AgentWorker>>()
            ));
        }
    })
    .UseSerilog((hostCtx, logConfig) => logConfig
        .ReadFrom.Configuration(hostCtx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console())
    .Build();

await host.RunAsync();