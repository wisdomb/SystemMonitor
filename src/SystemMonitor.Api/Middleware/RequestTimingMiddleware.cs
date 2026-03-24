using System.Diagnostics;

namespace SystemMonitor.Api.Middleware;

public class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            if (!context.Response.HasStarted)
                context.Response.Headers["X-Response-Time-Ms"] = sw.ElapsedMilliseconds.ToString();

            if (sw.ElapsedMilliseconds > 2000)
                _logger.LogWarning(
                    "Slow request: {Method} {Path} took {Elapsed}ms",
                    context.Request.Method,
                    context.Request.Path,
                    sw.ElapsedMilliseconds);
        }
    }
}
