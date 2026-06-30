using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Observability.Local;

public class LocalMetricsService : IMetricsService
{
    private const int MaxRecentEntries = 500;

    private readonly ILogger<LocalMetricsService> _logger;
    private readonly ICorrelationContextAccessor _correlation;
    private readonly ConcurrentQueue<MetricEntry> _recent = new();

    public LocalMetricsService(ILogger<LocalMetricsService> logger, ICorrelationContextAccessor correlation)
    {
        _logger = logger;
        _correlation = correlation;
    }

    public IReadOnlyCollection<MetricEntry> RecentEntries => _recent.ToArray();

    public void Increment(string name, double value = 1, IReadOnlyDictionary<string, string>? tags = null) =>
        Record(MetricKind.Counter, name, value, tags);

    public void RecordValue(string name, double value, IReadOnlyDictionary<string, string>? tags = null) =>
        Record(MetricKind.Value, name, value, tags);

    private void Record(MetricKind kind, string name, double value, IReadOnlyDictionary<string, string>? tags)
    {
        var entry = new MetricEntry(kind, name, value, _correlation.CorrelationId, _correlation.UserId, tags, DateTimeOffset.UtcNow);

        _recent.Enqueue(entry);
        while (_recent.Count > MaxRecentEntries) _recent.TryDequeue(out _);

        _logger.LogInformation("[{CorrelationId}] [{UserId}] metric {Kind} {Name}={Value} {Tags}",
            entry.CorrelationId, entry.UserId, kind, name, value, Format(tags));
    }

    private static string Format(IReadOnlyDictionary<string, string>? tags) =>
        tags is null || tags.Count == 0
            ? string.Empty
            : string.Join(", ", tags.Select(t => $"{t.Key}={t.Value}"));
}
