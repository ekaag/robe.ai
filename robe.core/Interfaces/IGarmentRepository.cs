using Robe.Core.Domain;

namespace Robe.Core.Interfaces;

public interface IGarmentRepository
{
    Task<Garment> AddAsync(Garment garment, CancellationToken ct = default);
    Task<Garment?> GetByIdAsync(string id, string userId, CancellationToken ct = default);
    Task<IReadOnlyList<Garment>> ListAsync(string userId, GarmentQuery query, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, string userId, CancellationToken ct = default);
}
