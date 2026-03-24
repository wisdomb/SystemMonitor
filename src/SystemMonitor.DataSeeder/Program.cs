using System.Text.Json;
using Spectre.Console;
using SystemMonitor.DataSeeder;
using SystemMonitor.Shared.Models;

string apiUrl = args.FirstOrDefault(a => a.StartsWith("--api="))?.Split('=')[1]
                 ?? "http://localhost:5000";
string mode = args.FirstOrDefault(a => a.StartsWith("--mode="))?.Split('=')[1]
                 ?? "interactive";
int agents = int.TryParse(args.FirstOrDefault(a => a.StartsWith("--agents="))?.Split('=')[1], out var ag) ? ag : 3;
int hours = int.TryParse(args.FirstOrDefault(a => a.StartsWith("--hours="))?.Split('=')[1], out var h) ? h : 24;

AnsiConsole.Write(new FigletText("System Monitor Seeder").Color(Color.Cyan1));
AnsiConsole.MarkupLine($"[grey]Target API:[/] [cyan]{apiUrl}[/]");

if (mode == "interactive")
{
    mode = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("[cyan]Select seeder mode:[/]")
            .AddChoices([
                "backfill   — push N hours of historical mock data (bulk, fast)",
                "stream     — continuously stream live mock data (real-time demo)",
                "training   — generate and upload labeled training CSV",
                "all        — backfill + then stream"
            ])
    ).Split(' ')[0];
}

using var http = new HttpClient { BaseAddress = new Uri(apiUrl), Timeout = TimeSpan.FromSeconds(30) };
using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

switch (mode)
{
    case "backfill":
        await BackfillAsync(http, agents, hours, cts.Token);
        break;
    case "stream":
        await StreamAsync(http, agents, cts.Token);
        break;
    case "training":
        await UploadTrainingDataAsync(http, cts.Token);
        break;
    case "all":
        await BackfillAsync(http, agents, hours, cts.Token);
        if (!cts.IsCancellationRequested)
            await StreamAsync(http, agents, cts.Token);
        break;
    default:
        AnsiConsole.MarkupLine($"[red]Unknown mode:[/] {mode}");
        break;
}

AnsiConsole.MarkupLine("\n[green]Done.[/]");

static async Task BackfillAsync(HttpClient http, int agentCount, int historyHours, CancellationToken ct)
{
    AnsiConsole.MarkupLine($"\n[cyan]Backfilling {historyHours}h of history for {agentCount} agent(s)…[/]");

    int totalTicks = historyHours * 360;
    var generators = Enumerable.Range(1, agentCount)
        .Select(i => (
            agentId: $"mock-agent-{i:D3}",
            metricGen: new MockGeneratorAdapter($"mock-agent-{i:D3}", $"host-{i:D2}"),
            logGen: new MockLogAdapter($"mock-agent-{i:D3}", $"host-{i:D2}")
        )).ToList();

    int sent = 0;
    int batchSize = 100;

    await AnsiConsole.Progress()
        .Columns(new TaskDescriptionColumn(), new ProgressBarColumn(),
                 new PercentageColumn(), new SpinnerColumn())
        .StartAsync(async ctx =>
        {
            var task = ctx.AddTask("[green]Backfilling[/]", maxValue: totalTicks * agentCount);
            var baseTime = DateTimeOffset.UtcNow.AddHours(-historyHours);

            for (int tick = 0; tick < totalTicks && !ct.IsCancellationRequested; tick++)
            {
                var timestamp = baseTime.AddSeconds(tick * 10);
                var metricBatch = new List<MetricEvent>();
                var logBatch = new List<LogEvent>();

                foreach (var (agentId, mg, lg) in generators)
                {
                    var m = mg.Generate(timestamp);
                    metricBatch.Add(m);
                    logBatch.AddRange(lg.GenerateBatch(timestamp));
                }

                if (metricBatch.Count >= batchSize || tick == totalTicks - 1)
                {
                    await PostAsync(http, "api/v1/ingest/metrics",
                        new IngestionBatch<MetricEvent> { Events = metricBatch }, ct);
                    await PostAsync(http, "api/v1/ingest/logs",
                        new IngestionBatch<LogEvent> { Events = logBatch }, ct);
                    sent += metricBatch.Count;
                }

                task.Increment(agentCount);
            }
        });
    AnsiConsole.MarkupLine($"[green]✓[/] Backfill complete — {sent:N0} metric events sent");
}

static async Task StreamAsync(HttpClient http, int agentCount, CancellationToken ct)
{
    AnsiConsole.MarkupLine($"\n[cyan]Streaming live mock data for {agentCount} agent(s). Press Ctrl+C to stop.[/]\n");

    var generators = Enumerable.Range(1, agentCount)
        .Select(i => (
            agentId: $"mock-agent-{i:D3}",
            mg: new MockGeneratorAdapter($"mock-agent-{i:D3}", $"host-{i:D2}"),
            lg: new MockLogAdapter($"mock-agent-{i:D3}", $"host-{i:D2}")
        )).ToList();

    var table = new Table().AddColumn("Time").AddColumn("Agent").AddColumn("CPU %")
        .AddColumn("Mem %").AddColumn("Err Rate").AddColumn("Anomaly?");

    long ticks = 0;
    while (!ct.IsCancellationRequested)
    {
        ticks++;
        var metricBatch = new List<MetricEvent>();
        var logBatch = new List<LogEvent>();
        var rows = new List<(string[] cols)>();

        foreach (var (agentId, mg, lg) in generators)
        {
            var m = mg.Generate(DateTimeOffset.UtcNow);
            metricBatch.Add(m);
            logBatch.AddRange(lg.GenerateBatch(DateTimeOffset.UtcNow));

            bool isAnomaly = m.Tags.GetValueOrDefault("is_anomaly") == "true";
            rows.Add(([
                DateTimeOffset.UtcNow.ToString("HH:mm:ss"),
                agentId,
                $"{m.Values["cpu_percent"]:F1}",
                $"{m.Values["memory_percent"]:F1}",
                $"{m.Values["error_rate"]:P2}",
                isAnomaly ? "[red]YES[/]" : "[green]no[/]"
            ]));
        }

        await PostAsync(http, "api/v1/ingest/metrics",
            new IngestionBatch<MetricEvent> { Events = metricBatch }, ct);
        await PostAsync(http, "api/v1/ingest/logs",
            new IngestionBatch<LogEvent> { Events = logBatch }, ct);

        foreach (var r in rows)
            AnsiConsole.MarkupLine(
                $"[grey]{r[0]}[/]  [cyan]{r[1]}[/]  cpu=[yellow]{r[2]}[/]  mem=[yellow]{r[3]}[/]  err=[yellow]{r[4]}[/]  anomaly={r[5]}");

        if (ticks % 6 == 0)
            AnsiConsole.MarkupLine($"[grey]  ── {ticks} ticks sent ──[/]");

        await Task.Delay(TimeSpan.FromSeconds(10), ct).ContinueWith(_ => { });
    }
}

static async Task UploadTrainingDataAsync(HttpClient http, CancellationToken ct)
{
    AnsiConsole.MarkupLine("\n[cyan]Generating labeled training data…[/]");

    int recordCount = AnsiConsole.Ask<int>(
        "How many training records to generate?", 2000);

    var csv = TrainingDataGenerator.GenerateCsv(recordCount);

    AnsiConsole.MarkupLine($"Generated [green]{recordCount:N0}[/] records. Uploading…");

    var content = new StringContent(csv, System.Text.Encoding.UTF8, "text/csv");
    var resp = await http.PostAsync("api/v1/ingest/training-data", content, ct);

    if (resp.IsSuccessStatusCode)
        AnsiConsole.MarkupLine($"[green]✓[/] Training data uploaded. Retraining will begin shortly.");
    else
        AnsiConsole.MarkupLine($"[red]✗[/] Upload failed: {resp.StatusCode}");
}

static async Task PostAsync<T>(HttpClient http, string path, T payload, CancellationToken ct)
{
    try
    {
        var json = JsonSerializer.Serialize(payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        await http.PostAsync(path, content, ct);
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        AnsiConsole.MarkupLine($"[red]POST failed:[/] {ex.Message}");
    }
}
