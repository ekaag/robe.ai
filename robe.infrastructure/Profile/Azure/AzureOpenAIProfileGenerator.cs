using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Profile.Azure;

public class AzureOpenAIProfileGenerator : IProfileGenerator
{
    public Task<StyleProfile> GenerateAsync(IReadOnlyCollection<GarmentTraits> traits, CancellationToken ct = default)
    {
        throw new NotImplementedException("AzureOpenAIProfileGenerator is not yet implemented.");
    }
}
