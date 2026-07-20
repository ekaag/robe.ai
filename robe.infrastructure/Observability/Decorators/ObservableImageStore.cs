using Robe.Core.Domain;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Decorators;

public class ObservableImageStore : IImageStore
{
    private readonly IImageStore _inner;
    private readonly ILogService _log;
    private readonly IMetricsService _metrics;
    private readonly IAlertService _alerts;

    public ObservableImageStore(IImageStore inner, ILogService log, IMetricsService metrics, IAlertService alerts)
    {
        _inner = inner;
        _log = log;
        _metrics = metrics;
        _alerts = alerts;
    }

    public async Task<string> SaveAsync(ImageInput image, string key, CancellationToken ct = default)
    {
        try
        {
            var url = await _inner.SaveAsync(image, key, ct);
            _metrics.Increment("images.stored");
            _log.Info("Image stored", new Dictionary<string, object?> { ["blobKey"] = key });
            return url;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Increment("images.store_failed");
            _log.Error("Image store failed", ex, new Dictionary<string, object?> { ["blobKey"] = key, ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message });
            await _alerts.RaiseAsync(AlertSeverity.Error, "Image store failed",
                new Dictionary<string, object?> { ["blobKey"] = key, ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message }, ct);
            throw;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await _inner.DeleteAsync(key, ct);
            _metrics.Increment("images.deleted");
            _log.Info("Image deleted", new Dictionary<string, object?> { ["blobKey"] = key });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Increment("images.delete_failed");
            _log.Error("Image delete failed", ex, new Dictionary<string, object?> { ["blobKey"] = key, ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message });
            await _alerts.RaiseAsync(AlertSeverity.Error, "Image delete failed",
                new Dictionary<string, object?> { ["blobKey"] = key, ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message }, ct);
            throw;
        }
    }
}
