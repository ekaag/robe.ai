namespace Robe.Infrastructure.Observability.Local;

public enum MetricKind { Counter, Value }

public sealed record MetricEntry(
    MetricKind Kind,
    string Name,
    double Value,
    string CorrelationId,
    string UserId,
    IReadOnlyDictionary<string, string>? Tags,
    DateTimeOffset Timestamp);
