using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Persistence;

public class SqlProfileRepository : IProfileRepository
{
    public Task SaveAsync(string userId, StyleProfile profile, CancellationToken ct = default)
        => throw new NotImplementedException("SqlProfileRepository is not yet implemented.");

    public Task<StyleProfile?> GetAsync(string userId, CancellationToken ct = default)
        => throw new NotImplementedException("SqlProfileRepository is not yet implemented.");
}
