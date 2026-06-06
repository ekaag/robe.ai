using Robe.Core.Domain;

namespace Robe.Core.Interfaces;

public interface ITraitsExtractor
{
    Task<GarmentTraits> ExtractAsync(ImageInput image, CancellationToken ct = default);
}
