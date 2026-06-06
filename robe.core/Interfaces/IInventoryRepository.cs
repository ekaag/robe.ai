using Robe.Core.Domain;

namespace Robe.Core.Interfaces;

public interface IInventoryRepository
{
    Task<IReadOnlyList<InventoryItem>> QueryAsync(InventoryQuery query, CancellationToken ct = default);
}
