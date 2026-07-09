using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.TraitsExtraction;

public class FakeFashionTraitsExtractor : IFashionTraitsExtractor
{
    private readonly Func<IReadOnlyCollection<FashionImageInput>, TraitsExtractionResult>? _factory;
    private readonly Exception? _exception;

    private FakeFashionTraitsExtractor() { }
    private FakeFashionTraitsExtractor(Func<IReadOnlyCollection<FashionImageInput>, TraitsExtractionResult> factory) => _factory = factory;
    private FakeFashionTraitsExtractor(Exception exception) => _exception = exception;

    public static FakeFashionTraitsExtractor ReturnsDefault() => new();
    public static FakeFashionTraitsExtractor Returns(Func<IReadOnlyCollection<FashionImageInput>, TraitsExtractionResult> factory) => new(factory);
    public static FakeFashionTraitsExtractor Throws(Exception exception) => new(exception);

    public Task<TraitsExtractionResult> ExtractTraitsAsync(
        IReadOnlyCollection<FashionImageInput> images,
        CancellationToken cancellationToken = default)
    {
        if (_exception is not null) throw _exception;
        if (_factory is not null) return Task.FromResult(_factory(images));
        return Task.FromResult(BuildDefaultResult(images));
    }

    public static TraitsExtractionResult BuildDefaultResult(IReadOnlyCollection<FashionImageInput> images)
    {
        var imageResults = images.Select(img => new ImageTraitsResult(
            img.ImageId,
            new List<PersonTraits>
            {
                new PersonTraits(
                    PersonId: "person-1",
                    Position: "center",
                    OverallStyle: new List<string> { "minimalist" },
                    StyleTags: new List<string> { "casual" },
                    ClothingItems: new List<ClothingItemTraits>
                    {
                        new ClothingItemTraits(
                            Category: "top",
                            Type: "t-shirt",
                            Subtype: null,
                            PrimaryColor: new ColorTraits("navy", "dark"),
                            SecondaryColors: new List<ColorTraits>(),
                            Pattern: "solid",
                            Material: "cotton",
                            Fit: "regular",
                            Length: null,
                            SleeveLength: "short",
                            Neckline: "crew",
                            CollarType: null,
                            WaistRise: null,
                            ClosureType: null,
                            Details: new List<string>(),
                            VisibleText: null,
                            Brand: null,
                            Logo: null,
                            Condition: null,
                            StyleTags: new List<string> { "minimalist" },
                            Confidence: 0.86)
                    },
                    OverallConfidence: 0.86)
            },
            Warnings: new List<string>()))
            .ToList();
        return new TraitsExtractionResult(imageResults);
    }
}
