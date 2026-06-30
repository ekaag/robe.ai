using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Local;

public class LocalAlertService : IAlertService
{
    private const int MaxRecentEntries = 200;

    private readonly ILogger<LocalAlertService> _logger;
    private readonly ICorrelationContextAccessor _correlation;
    private readonly ConcurrentQueue<AlertEntry> _recent = new();

    public LocalAlertService(ILogger<LocalAlertService> logger, ICorrelationContextAccessor correlation)
    {
        _logger = logger;
        _correlation = correlation;
    }

    public IReadOnlyCollection<AlertEntry> RecentEntries => _recent.ToArray();

    public Task RaiseAsync(
        AlertSeverity severity,
        string message,
        IReadOnlyDictionary<string, object?>? context = null,
        CancellationToken ct = default)
    {
        var entry = new AlertEntry(severity, message, _correlation.CorrelationId, _correlation.UserId, context, DateTimeOffset.UtcNow);

        _recent.Enqueue(entry);
        while (_recent.Count > MaxRecentEntries) _recent.TryDequeue(out _);

        _logger.LogCritical("ALERT [{Severity}] [{CorrelationId}] [{UserId}] {Message} {Context}",
            severity, entry.CorrelationId, entry.UserId, message, Format(context));

        return Task.CompletedTask;
    }

    private static string Format(IReadOnlyDictionary<string, object?>? context) =>
        context is null || context.Count == 0
            ? string.Empty
            : string.Join(", ", context.Select(c => $"{c.Key}={c.Value}"));
}
