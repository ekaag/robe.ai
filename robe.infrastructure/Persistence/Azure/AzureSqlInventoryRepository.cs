using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Persistence.Azure;

public class AzureSqlInventoryRepository : IInventoryRepository
{
    public Task<IReadOnlyList<InventoryItem>> QueryAsync(InventoryQuery query, CancellationToken ct = default)
        => throw new NotImplementedException("AzureSqlInventoryRepository is not yet implemented.");
}
