using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Recommendations;

public class FakeRecommender : IRecommender
{
    public Task<IReadOnlyList<Recommendation>> RecommendAsync(
        StyleProfile profile,
        IReadOnlyList<InventoryItem> candidates,
        RecommendationContext context,
        CancellationToken ct = default)
    {
        var results = candidates
            .Select(item =>
            {
                var overlap = item.Traits.StyleTags
                    .Intersect(profile.DominantStyles, StringComparer.OrdinalIgnoreCase)
                    .Count();
                var score = Math.Min(1.0,
                    (double)overlap / Math.Max(1, item.Traits.StyleTags.Count) + 0.1);
                return new Recommendation
                {
                    InventoryItem = item,
                    Score         = Math.Round(score, 2),
                    Reasoning     = $"Matched {overlap} style tag(s) with your profile."
                };
            })
            .OrderByDescending(r => r.Score)
            .Take(context.Count)
            .ToList();

        return Task.FromResult<IReadOnlyList<Recommendation>>(results);
    }
}
