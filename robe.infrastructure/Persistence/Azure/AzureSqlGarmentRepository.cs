using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Persistence.Azure;

// Stub — wire up EF Core + Azure SQL when deploying to cloud.
public class AzureSqlGarmentRepository : IGarmentRepository
{
    public Task<Garment> AddAsync(Garment garment, CancellationToken ct = default) =>
        throw new NotImplementedException("AzureSqlGarmentRepository requires Azure SQL configuration.");

    public Task<Garment?> GetByIdAsync(string id, string userId, CancellationToken ct = default) =>
        throw new NotImplementedException("AzureSqlGarmentRepository requires Azure SQL configuration.");

    public Task<IReadOnlyList<Garment>> ListAsync(string userId, GarmentQuery query, CancellationToken ct = default) =>
        throw new NotImplementedException("AzureSqlGarmentRepository requires Azure SQL configuration.");

    public Task<bool> DeleteAsync(string id, string userId, CancellationToken ct = default) =>
        throw new NotImplementedException("AzureSqlGarmentRepository requires Azure SQL configuration.");
}
