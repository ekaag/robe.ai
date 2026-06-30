using System.Diagnostics;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Api.Middleware;

// First middleware in the pipeline (before CORS/auth) so every request — including
// failed preflights and 401s — gets a correlation ID and HTTP metrics. Reuses the
// client-supplied X-Correlation-Id when present so callers can tie their own logs
// to ours; otherwise mints a new one.
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        ICorrelationContextAccessor correlation,
        ILogService log,
        IMetricsService metrics)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
            && !string.IsNullOrWhiteSpace(existing)
                ? existing.ToString()
                : Guid.NewGuid().ToString();

        correlation.CorrelationId = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var tags = new Dictionary<string, string>
            {
                ["method"] = context.Request.Method,
                ["statusCode"] = context.Response.StatusCode.ToString()
            };
            metrics.RecordValue("http.request_duration_ms", stopwatch.Elapsed.TotalMilliseconds, tags);
            metrics.Increment("http.requests", 1, tags);

            log.Info("HTTP request completed", new Dictionary<string, object?>
            {
                ["method"] = context.Request.Method,
                ["path"] = context.Request.Path.ToString(),
                ["statusCode"] = context.Response.StatusCode,
                ["durationMs"] = stopwatch.Elapsed.TotalMilliseconds
            });
        }
    }
}
