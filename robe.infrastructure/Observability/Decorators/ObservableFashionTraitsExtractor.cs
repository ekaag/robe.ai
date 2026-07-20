using Robe.Core.Domain;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Decorators;

public class ObservableFashionTraitsExtractor : IFashionTraitsExtractor
{
    private readonly IFashionTraitsExtractor _inner;
    private readonly ILogService _log;
    private readonly IMetricsService _metrics;
    private readonly IAlertService _alerts;

    public ObservableFashionTraitsExtractor(IFashionTraitsExtractor inner, ILogService log, IMetricsService metrics, IAlertService alerts)
    {
        _inner = inner;
        _log = log;
        _metrics = metrics;
        _alerts = alerts;
    }

    public async Task<TraitsExtractionResult> ExtractTraitsAsync(
        IReadOnlyCollection<FashionImageInput> images,
        CancellationToken cancellationToken = default)
    {
        // Tagged by imageCount so duration can be correlated against batch size later
        // (e.g. in App Insights) without having to cross-reference separate logs.
        var tags = new Dictionary<string, string> { ["imageCount"] = images.Count.ToString() };
        using var timer = _metrics.Time("fashion_traits.extract_duration_ms", tags);
        try
        {
            var result = await _inner.ExtractTraitsAsync(images, cancellationToken);
            var personCount = result.Images.Sum(i => i.People.Count);
            var itemCount = result.Images.Sum(i => i.People.Sum(p => p.ClothingItems.Count));

            _metrics.Increment("fashion_traits.analyzed", tags: tags);
            _metrics.RecordValue("fashion_traits.people_detected", personCount, tags);
            _metrics.RecordValue("fashion_traits.items_detected", itemCount, tags);
            _log.Info("Fashion traits extracted", new Dictionary<string, object?>
            {
                ["imageCount"] = images.Count,
                ["personCount"] = personCount,
                ["itemCount"] = itemCount,
            });
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Increment("fashion_traits.failed", tags: tags);
            _log.Error("Fashion traits extraction failed", ex, new Dictionary<string, object?> { ["imageCount"] = images.Count });
            await _alerts.RaiseAsync(
                AlertSeverity.Error,
                "Fashion traits extraction failed",
                new Dictionary<string, object?> { ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message, ["imageCount"] = images.Count },
                cancellationToken);
            throw;
        }
    }
}
