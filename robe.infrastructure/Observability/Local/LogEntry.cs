using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Local;

public sealed record LogEntry(
    LogSeverity Severity,
    string Message,
    string CorrelationId,
    IReadOnlyDictionary<string, object?>? Properties,
    Exception? Exception,
    DateTimeOffset Timestamp);
