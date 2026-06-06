using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Persistence;

public class InMemoryInventoryRepository : IInventoryRepository
{
    private readonly List<InventoryItem> _items;

    public InMemoryInventoryRepository(IEnumerable<InventoryItem>? seed = null)
        => _items = seed?.ToList() ?? new List<InventoryItem>();

    public Task<IReadOnlyList<InventoryItem>> QueryAsync(InventoryQuery query, CancellationToken ct = default)
    {
        IEnumerable<InventoryItem> results = _items;

        if (query.InStockOnly)
            results = results.Where(i => i.InStock);

        if (query.Categories is { Count: > 0 })
            results = results.Where(i => query.Categories.Contains(i.Traits.Category));

        if (query.MaxPrice.HasValue)
            results = results.Where(i => i.Price <= query.MaxPrice.Value);

        if (query.Occasion is not null)
            results = results.Where(i =>
                i.Traits.Occasions.Contains(query.Occasion, StringComparer.OrdinalIgnoreCase));

        return Task.FromResult<IReadOnlyList<InventoryItem>>(results.ToList());
    }
}
