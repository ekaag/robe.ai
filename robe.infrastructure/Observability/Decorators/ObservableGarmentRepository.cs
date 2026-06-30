using Robe.Core.Domain;
using Robe.Core.Interfaces;
using Robe.Core.Observability;

namespace Robe.Infrastructure.Observability.Decorators;

public class ObservableGarmentRepository : IGarmentRepository
{
    private readonly IGarmentRepository _inner;
    private readonly ILogService _log;
    private readonly IMetricsService _metrics;

    public ObservableGarmentRepository(IGarmentRepository inner, ILogService log, IMetricsService metrics)
    {
        _inner = inner;
        _log = log;
        _metrics = metrics;
    }

    public async Task<Garment> AddAsync(Garment garment, CancellationToken ct = default)
    {
        var result = await _inner.AddAsync(garment, ct);
        _metrics.Increment("garments.stored");
        _log.Info("Garment stored", new Dictionary<string, object?> { ["garmentId"] = result.Id });
        return result;
    }

    public Task<Garment?> GetByIdAsync(string id, string userId, CancellationToken ct = default) =>
        _inner.GetByIdAsync(id, userId, ct);

    public Task<IReadOnlyList<Garment>> ListAsync(string userId, GarmentQuery query, CancellationToken ct = default) =>
        _inner.ListAsync(userId, query, ct);

    public async Task<bool> DeleteAsync(string id, string userId, CancellationToken ct = default)
    {
        var deleted = await _inner.DeleteAsync(id, userId, ct);
        if (deleted)
        {
            _metrics.Increment("garments.deleted");
            _log.Info("Garment deleted", new Dictionary<string, object?> { ["garmentId"] = id });
        }
        return deleted;
    }
}
