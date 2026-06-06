using System.Collections.Concurrent;
using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Persistence;

public class InMemoryGarmentRepository : IGarmentRepository
{
    private readonly ConcurrentDictionary<string, Garment> _store = new();

    public Task<Garment> AddAsync(Garment garment, CancellationToken ct = default)
    {
        _store[garment.Id] = garment;
        return Task.FromResult(garment);
    }

    public Task<Garment?> GetByIdAsync(string id, string userId, CancellationToken ct = default)
    {
        _store.TryGetValue(id, out var garment);
        var result = garment is not null && garment.UserId == userId ? garment : null;
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<Garment>> ListAsync(string userId, GarmentQuery query, CancellationToken ct = default)
    {
        var items = _store.Values
            .Where(g => g.UserId == userId)
            .Where(g => query.Category == null || g.Traits.Category == query.Category)
            .OrderBy(g => g.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToList();

        return Task.FromResult<IReadOnlyList<Garment>>(items);
    }

    public Task<bool> DeleteAsync(string id, string userId, CancellationToken ct = default)
    {
        if (!_store.TryGetValue(id, out var garment) || garment.UserId != userId)
            return Task.FromResult(false);

        return Task.FromResult(_store.TryRemove(id, out _));
    }
}
