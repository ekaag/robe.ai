using Robe.Core.Domain;

namespace Robe.Core.Interfaces;

public interface IProfileRepository
{
    Task SaveAsync(string userId, StyleProfile profile, CancellationToken ct = default);
    Task<StyleProfile?> GetAsync(string userId, CancellationToken ct = default);
}
