using Robe.Core.Domain;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Decorators;

public class ObservableProfileGenerator : IProfileGenerator
{
    private readonly IProfileGenerator _inner;
    private readonly ILogService _log;
    private readonly IMetricsService _metrics;
    private readonly IAlertService _alerts;

    public ObservableProfileGenerator(IProfileGenerator inner, ILogService log, IMetricsService metrics, IAlertService alerts)
    {
        _inner = inner;
        _log = log;
        _metrics = metrics;
        _alerts = alerts;
    }

    public async Task<StyleProfile> GenerateAsync(IReadOnlyCollection<GarmentTraits> traits, CancellationToken ct = default)
    {
        using var timer = _metrics.Time("profile.generate_duration_ms");
        try
        {
            var profile = await _inner.GenerateAsync(traits, ct);
            _metrics.Increment("profile.generated");
            _log.Info("Style profile generated", new Dictionary<string, object?> { ["garmentCount"] = traits.Count });
            return profile;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Increment("profile.generation_failed");
            _log.Error("Style profile generation failed", ex);
            await _alerts.RaiseAsync(
                AlertSeverity.Error,
                "Style profile generation failed",
                new Dictionary<string, object?> { ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message },
                ct);
            throw;
        }
    }
}
