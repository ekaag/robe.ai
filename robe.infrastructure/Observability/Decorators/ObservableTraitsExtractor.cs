using Robe.Core.Domain;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Decorators;

public class ObservableTraitsExtractor : ITraitsExtractor
{
    private readonly ITraitsExtractor _inner;
    private readonly ILogService _log;
    private readonly IMetricsService _metrics;
    private readonly IAlertService _alerts;

    public ObservableTraitsExtractor(ITraitsExtractor inner, ILogService log, IMetricsService metrics, IAlertService alerts)
    {
        _inner = inner;
        _log = log;
        _metrics = metrics;
        _alerts = alerts;
    }

    public async Task<GarmentTraits> ExtractAsync(ImageInput image, CancellationToken ct = default)
    {
        using var timer = _metrics.Time("garment_traits.extract_duration_ms");
        try
        {
            var traits = await _inner.ExtractAsync(image, ct);
            _metrics.Increment("garment_traits.analyzed");
            _log.Info("Garment traits extracted", new Dictionary<string, object?> { ["category"] = traits.Category });
            return traits;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Increment("garment_traits.failed");
            _log.Error("Garment trait extraction failed", ex);
            await _alerts.RaiseAsync(
                AlertSeverity.Error,
                "Garment trait extraction failed",
                new Dictionary<string, object?> { ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message },
                ct);
            throw;
        }
    }
}
