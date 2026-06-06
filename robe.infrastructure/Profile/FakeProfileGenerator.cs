using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Profile;

public class FakeProfileGenerator : IProfileGenerator
{
    public Task<StyleProfile> GenerateAsync(IReadOnlyCollection<GarmentTraits> traits, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        if (traits.Count == 0)
        {
            return Task.FromResult(new StyleProfile
            {
                DominantStyles  = new List<string>(),
                ColorPalette    = new List<ColorWeight>(),
                FormalityRange  = new FormalityRange(0, 0, 0),
                PreferredFits   = new List<string>(),
                SeasonalSkew    = new Dictionary<string, double>(),
                Summary         = "Insufficient data to generate a style profile.",
                GarmentCount    = 0,
                CreatedAt       = now,
                ModifiedAt      = now,
                CreatedByUserId  = "",
                ModifiedByUserId = ""
            });
        }

        var dominantStyles = traits
            .SelectMany(t => t.StyleTags)
            .GroupBy(s => s)
            .OrderByDescending(g => g.Count())
            .Take(3)
            .Select(g => g.Key)
            .ToList();

        var colorGroups = traits
            .Select(t => t.PrimaryColor)
            .Where(c => c is not null)
            .GroupBy(c => c.Hex.ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToList();

        var totalColors = colorGroups.Sum(g => g.Count());
        var colorPalette = colorGroups
            .Select(g => new ColorWeight(
                g.First().Name,
                g.First().Hex,
                totalColors > 0 ? Math.Round((double)g.Count() / totalColors, 2) : 0))
            .ToList();

        var formalityValues = traits.Select(t => t.Formality).ToList();
        var formalityRange = new FormalityRange(
            formalityValues.Min(),
            formalityValues.Max(),
            Math.Round(formalityValues.Average(), 1));

        var preferredFits = traits
            .Where(t => t.Fit.HasValue)
            .GroupBy(t => t.Fit!.Value.ToString().ToLowerInvariant())
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToList();

        var seasonCounts = traits
            .SelectMany(t => t.Seasonality)
            .GroupBy(s => s.ToString().ToLowerInvariant())
            .ToDictionary(g => g.Key, g => (double)g.Count());

        var seasonTotal = seasonCounts.Values.Sum();
        var seasonalSkew = seasonCounts.ToDictionary(
            kv => kv.Key,
            kv => seasonTotal > 0 ? Math.Round(kv.Value / seasonTotal, 2) : 0);

        return Task.FromResult(new StyleProfile
        {
            DominantStyles   = dominantStyles,
            ColorPalette     = colorPalette,
            FormalityRange   = formalityRange,
            PreferredFits    = preferredFits,
            SeasonalSkew     = seasonalSkew,
            Summary          = $"Wardrobe of {traits.Count} garment(s). Leans {string.Join(", ", dominantStyles.Take(2))}.",
            GarmentCount     = traits.Count,
            CreatedAt        = now,
            ModifiedAt       = now,
            CreatedByUserId  = "",
            ModifiedByUserId = ""
        });
    }
}
