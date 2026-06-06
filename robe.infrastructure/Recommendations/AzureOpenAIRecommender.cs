using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Recommendations;

public class AzureOpenAIRecommender : IRecommender
{
    public Task<IReadOnlyList<Recommendation>> RecommendAsync(
        StyleProfile profile,
        IReadOnlyList<InventoryItem> candidates,
        RecommendationContext context,
        CancellationToken ct = default)
    {
        throw new NotImplementedException("AzureOpenAIRecommender is not yet implemented.");
    }
}
