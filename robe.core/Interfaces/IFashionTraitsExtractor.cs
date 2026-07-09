using Robe.Core.Domain;

namespace Robe.Core.Interfaces;

public interface IFashionTraitsExtractor
{
    Task<TraitsExtractionResult> ExtractTraitsAsync(
        IReadOnlyCollection<FashionImageInput> images,
        CancellationToken cancellationToken = default);
}
