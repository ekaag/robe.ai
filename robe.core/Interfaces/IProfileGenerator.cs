using Robe.Core.Domain;

namespace Robe.Core.Interfaces;

public interface IProfileGenerator
{
    Task<StyleProfile> GenerateAsync(IReadOnlyCollection<GarmentTraits> traits, CancellationToken ct = default);
}
