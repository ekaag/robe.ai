using System.Collections.Concurrent;
using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Persistence;

public class InMemoryProfileRepository : IProfileRepository
{
    private readonly ConcurrentDictionary<string, StyleProfile> _store = new();

    public Task SaveAsync(string userId, StyleProfile profile, CancellationToken ct = default)
    {
        _store.AddOrUpdate(userId, profile, (_, existing) =>
        {
            // Preserve create-fields on regeneration; update modify-fields from the new profile.
            return new StyleProfile
            {
                DominantStyles   = profile.DominantStyles,
                ColorPalette     = profile.ColorPalette,
                FormalityRange   = profile.FormalityRange,
                PreferredFits    = profile.PreferredFits,
                SeasonalSkew     = profile.SeasonalSkew,
                Summary          = profile.Summary,
                GarmentCount     = profile.GarmentCount,
                CreatedAt        = existing.CreatedAt,
                CreatedByUserId  = existing.CreatedByUserId,
                ModifiedAt       = profile.ModifiedAt,
                ModifiedByUserId = profile.ModifiedByUserId
            };
        });
        return Task.CompletedTask;
    }

    public Task<StyleProfile?> GetAsync(string userId, CancellationToken ct = default)
    {
        _store.TryGetValue(userId, out var profile);
        return Task.FromResult(profile);
    }
}
