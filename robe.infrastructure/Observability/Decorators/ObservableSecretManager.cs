using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Decorators;

public class ObservableSecretManager : ISecretManager
{
    private readonly ISecretManager _inner;
    private readonly ILogService _log;
    private readonly IMetricsService _metrics;
    private readonly IAlertService _alerts;

    public ObservableSecretManager(ISecretManager inner, ILogService log, IMetricsService metrics, IAlertService alerts)
    {
        _inner = inner;
        _log = log;
        _metrics = metrics;
        _alerts = alerts;
    }

    public async Task<string> GetSecretAsync(string name, CancellationToken ct = default)
    {
        using var timer = _metrics.Time("secret.fetch_duration_ms");
        try
        {
            var value = await _inner.GetSecretAsync(name, ct);
            _metrics.Increment("secret.fetched");
            return value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Increment("secret.fetch_failed");
            _log.Error("Secret fetch failed", ex, new Dictionary<string, object?> { ["secretName"] = name });
            await _alerts.RaiseAsync(
                AlertSeverity.Error,
                "Secret fetch failed",
                new Dictionary<string, object?> { ["secretName"] = name, ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message },
                ct);
            throw;
        }
    }
}
