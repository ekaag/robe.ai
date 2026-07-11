using Robe.Core.Domain;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Decorators;

public class ObservableGarmentRepository : IGarmentRepository
{
    private readonly IGarmentRepository _inner;
    private readonly ILogService _log;
    private readonly IMetricsService _metrics;
    private readonly IAlertService _alerts;

    public ObservableGarmentRepository(IGarmentRepository inner, ILogService log, IMetricsService metrics, IAlertService alerts)
    {
        _inner = inner;
        _log = log;
        _metrics = metrics;
        _alerts = alerts;
    }

    public async Task<Garment> AddAsync(Garment garment, CancellationToken ct = default)
    {
        try
        {
            var result = await _inner.AddAsync(garment, ct);
            _metrics.Increment("garments.stored");
            _log.Info("Garment stored", new Dictionary<string, object?> { ["garmentId"] = result.Id });
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Increment("garments.store_failed");
            _log.Error("Garment store failed", ex, new Dictionary<string, object?> { ["garmentId"] = garment.Id, ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message });
            await _alerts.RaiseAsync(AlertSeverity.Error, "Garment store failed",
                new Dictionary<string, object?> { ["garmentId"] = garment.Id, ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message }, ct);
            throw;
        }
    }

    public async Task<Garment?> GetByIdAsync(string id, string userId, CancellationToken ct = default)
    {
        try
        {
            return await _inner.GetByIdAsync(id, userId, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Increment("garments.read_failed");
            _log.Error("Garment read failed", ex, new Dictionary<string, object?> { ["garmentId"] = id, ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message });
            await _alerts.RaiseAsync(AlertSeverity.Error, "Garment read failed",
                new Dictionary<string, object?> { ["garmentId"] = id, ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message }, ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<Garment>> ListAsync(string userId, GarmentQuery query, CancellationToken ct = default)
    {
        try
        {
            return await _inner.ListAsync(userId, query, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Increment("garments.list_failed");
            _log.Error("Garment list failed", ex, new Dictionary<string, object?> { ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message });
            await _alerts.RaiseAsync(AlertSeverity.Error, "Garment list failed",
                new Dictionary<string, object?> { ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message }, ct);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string id, string userId, CancellationToken ct = default)
    {
        try
        {
            var deleted = await _inner.DeleteAsync(id, userId, ct);
            if (deleted)
            {
                _metrics.Increment("garments.deleted");
                _log.Info("Garment deleted", new Dictionary<string, object?> { ["garmentId"] = id });
            }
            return deleted;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Increment("garments.delete_failed");
            _log.Error("Garment delete failed", ex, new Dictionary<string, object?> { ["garmentId"] = id, ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message });
            await _alerts.RaiseAsync(AlertSeverity.Error, "Garment delete failed",
                new Dictionary<string, object?> { ["garmentId"] = id, ["exception"] = ex.GetType().Name, ["exceptionMessage"] = ex.Message }, ct);
            throw;
        }
    }
}
