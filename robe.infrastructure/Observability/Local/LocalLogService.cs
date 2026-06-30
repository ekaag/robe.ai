using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Local;

// Writes structured entries to the standard .NET logger (console in local dev)
// and keeps a bounded in-memory buffer so tests can assert on what was logged.
public class LocalLogService : ILogService
{
    private const int MaxRecentEntries = 200;

    private readonly ILogger<LocalLogService> _logger;
    private readonly ICorrelationContextAccessor _correlation;
    private readonly ConcurrentQueue<LogEntry> _recent = new();

    public LocalLogService(ILogger<LocalLogService> logger, ICorrelationContextAccessor correlation)
    {
        _logger = logger;
        _correlation = correlation;
    }

    public IReadOnlyCollection<LogEntry> RecentEntries => _recent.ToArray();

    public void Log(
        LogSeverity severity,
        string message,
        IReadOnlyDictionary<string, object?>? properties = null,
        Exception? exception = null)
    {
        var entry = new LogEntry(severity, message, _correlation.CorrelationId, _correlation.UserId, properties, exception, DateTimeOffset.UtcNow);

        _recent.Enqueue(entry);
        while (_recent.Count > MaxRecentEntries) _recent.TryDequeue(out _);

        _logger.Log(ToLogLevel(severity), exception, "[{CorrelationId}] [{UserId}] {Message} {Properties}",
            entry.CorrelationId, entry.UserId, message, Format(properties));
    }

    private static string Format(IReadOnlyDictionary<string, object?>? properties) =>
        properties is null || properties.Count == 0
            ? string.Empty
            : string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}"));

    private static LogLevel ToLogLevel(LogSeverity severity) => severity switch
    {
        LogSeverity.Trace => LogLevel.Trace,
        LogSeverity.Debug => LogLevel.Debug,
        LogSeverity.Information => LogLevel.Information,
        LogSeverity.Warning => LogLevel.Warning,
        LogSeverity.Error => LogLevel.Error,
        LogSeverity.Critical => LogLevel.Critical,
        _ => LogLevel.Information
    };
}
