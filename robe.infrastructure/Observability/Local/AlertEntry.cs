using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Local;

public sealed record AlertEntry(
    AlertSeverity Severity,
    string Message,
    string CorrelationId,
    IReadOnlyDictionary<string, object?>? Context,
    DateTimeOffset Timestamp);
