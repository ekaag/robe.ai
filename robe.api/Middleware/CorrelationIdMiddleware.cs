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
        IMetricsService metrics,
        ICurrentUser currentUser)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var existing)
            && !string.IsNullOrWhiteSpace(existing)
                ? existing.ToString()
                : Guid.NewGuid().ToString();

        correlation.CorrelationId = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        log.Info("HTTP request started", new Dictionary<string, object?>
        {
            ["method"] = context.Request.Method,
            ["path"] = context.Request.Path.ToString(),
            ["query"] = context.Request.QueryString.ToString()
        });

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            // AsyncLocal mutations made deeper in the pipeline (e.g. by
            // UserContextMiddleware, after this middleware's own await) aren't
            // visible up here — re-resolve from the now-authenticated HttpContext
            // so this completion log/metric still carries the caller's user id.
            if (context.User.Identity?.IsAuthenticated == true)
            {
                try { correlation.UserId = currentUser.UserId; }
                catch (InvalidOperationException) { /* claims missing despite IsAuthenticated — leave UserId empty */ }
            }

            var statusCode = context.Response.StatusCode;
            var tags = new Dictionary<string, string>
            {
                ["method"] = context.Request.Method,
                ["statusCode"] = statusCode.ToString()
            };
            metrics.RecordValue("http.request_duration_ms", stopwatch.Elapsed.TotalMilliseconds, tags);
            metrics.Increment("http.requests", 1, tags);

            var completionProperties = new Dictionary<string, object?>
            {
                ["method"] = context.Request.Method,
                ["path"] = context.Request.Path.ToString(),
                ["statusCode"] = statusCode,
                ["durationMs"] = stopwatch.Elapsed.TotalMilliseconds
            };

            if (statusCode >= 500)
                log.Error("HTTP request completed with server error", properties: completionProperties);
            else if (statusCode >= 400)
                log.Warn("HTTP request completed with client error", completionProperties);
            else
                log.Info("HTTP request completed", completionProperties);
        }
    }
}
