using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.TraitsExtraction;

public class FakeTraitsExtractor : ITraitsExtractor
{
    private readonly GarmentTraits? _traits;
    private readonly Exception? _exception;

    private FakeTraitsExtractor() { }
    private FakeTraitsExtractor(GarmentTraits traits) => _traits = traits;
    private FakeTraitsExtractor(Exception exception) => _exception = exception;

    public static FakeTraitsExtractor ReturnsDefault() => new();
    public static FakeTraitsExtractor Returns(GarmentTraits traits) => new(traits);
    public static FakeTraitsExtractor Throws(Exception exception) => new(exception);

    public Task<GarmentTraits> ExtractAsync(ImageInput image, CancellationToken ct = default)
    {
        if (_exception is not null) throw _exception;
        return Task.FromResult(_traits ?? BuildDefaultTraits());
    }

    public static GarmentTraits BuildDefaultTraits() => new GarmentTraits
    {
        Category = GarmentCategory.Top,
        Subcategory = "t-shirt",
        PrimaryColor = new ColorInfo("navy", "#1f2a44"),
        SecondaryColors = new List<ColorInfo>(),
        Pattern = GarmentPattern.Solid,
        Material = "cotton",
        Fit = GarmentFit.Regular,
        Formality = 2,
        Seasonality = new List<Season> { Season.Spring, Season.Summer },
        StyleTags = new List<string> { "minimalist", "casual" },
        Occasions = new List<string> { "everyday" },
        Confidence = 0.86
    };
}
