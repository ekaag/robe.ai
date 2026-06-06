using Robe.Core.Domain;

namespace Robe.Core.Interfaces;

public interface IRecommender
{
    Task<IReadOnlyList<Recommendation>> RecommendAsync(
        StyleProfile profile,
        IReadOnlyList<InventoryItem> candidates,
        RecommendationContext context,
        CancellationToken ct = default);
}
