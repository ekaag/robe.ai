using Robe.Core.Domain;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Decorators;

public class ObservableRecommender : IRecommender
{
    private readonly IRecommender _inner;
    private readonly ILogService _log;
    private readonly IMetricsService _metrics;
    private readonly IAlertService _alerts;

    public ObservableRecommender(IRecommender inner, ILogService log, IMetricsService metrics, IAlertService alerts)
    {
        _inner = inner;
        _log = log;
        _metrics = metrics;
        _alerts = alerts;
    }

    public async Task<IReadOnlyList<Recommendation>> RecommendAsync(
        StyleProfile profile,
        IReadOnlyList<InventoryItem> candidates,
        RecommendationContext context,
        CancellationToken ct = default)
    {
        using var timer = _metrics.Time("recommendations.rank_duration_ms");
        try
        {
            var recommendations = await _inner.RecommendAsync(profile, candidates, context, ct);
            _metrics.Increment("recommendations.served", recommendations.Count);
            if (recommendations.Count > 0)
                _metrics.RecordValue("recommendations.avg_score", recommendations.Average(r => r.Score));
            _log.Info("Recommendations generated", new Dictionary<string, object?> { ["count"] = recommendations.Count });
            return recommendations;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Increment("recommendations.failed");
            _log.Error("Recommendation ranking failed", ex);
            await _alerts.RaiseAsync(
                AlertSeverity.Error,
                "Recommendation ranking failed",
                new Dictionary<string, object?> { ["exception"] = ex.GetType().Name },
                ct);
            throw;
        }
    }
}
