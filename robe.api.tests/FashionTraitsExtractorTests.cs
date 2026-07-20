using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Robe.Core.Domain;
using Robe.Core.Exceptions;
using Robe.Infrastructure.TraitsExtraction.Azure;

namespace Robe.Api.Tests;

// ---------------------------------------------------------------------------
// Fake adapter — returns canned responses, no Azure SDK types needed.
// ---------------------------------------------------------------------------
internal sealed class FakeAzureOpenAIChatAdapter : IAzureOpenAIChatAdapter
{
    private readonly string? _content;
    private readonly bool _isFiltered;
    private readonly Exception? _exception;

    private FakeAzureOpenAIChatAdapter(string? content, bool isFiltered, Exception? exception)
    {
        _content = content;
        _isFiltered = isFiltered;
        _exception = exception;
    }

    public static FakeAzureOpenAIChatAdapter Returns(string json) => new(json, false, null);
    public static FakeAzureOpenAIChatAdapter ReturnsEmpty() => new(null, false, null);
    public static FakeAzureOpenAIChatAdapter ReturnsFiltered() => new(null, true, null);
    public static FakeAzureOpenAIChatAdapter Throws(Exception ex) => new(null, false, ex);

    public Task<ChatAdapterResponse> CompleteChatAsync(ChatAdapterRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (_exception is not null) throw _exception;
        return Task.FromResult(new ChatAdapterResponse(_content, _isFiltered));
    }
}

// ---------------------------------------------------------------------------
// Helpers shared across test methods
// ---------------------------------------------------------------------------
file static class TestHelpers
{
    public static AzureOpenAITraitsExtractor Create(
        IAzureOpenAIChatAdapter adapter,
        AzureOpenAIOptions? opts = null)
    {
        opts ??= DefaultOptions();
        return new AzureOpenAITraitsExtractor(
            Options.Create(opts),
            NullLogger<AzureOpenAITraitsExtractor>.Instance,
            adapter);
    }

    public static AzureOpenAIOptions DefaultOptions() => new()
    {
        MaxImages = 5,
        MaxImageSizeBytes = 5L * 1024 * 1024,
    };

    public static byte[] SmallImage() => new byte[1024];

    public static FashionImageInput Image(string id, string mime = "image/jpeg")
        => new(id, SmallImage(), mime);
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------
public class FashionTraitsExtractorTests
{
    // 1. Empty image collection
    [Fact]
    public async Task ExtractTraitsAsync_EmptyCollection_ThrowsImageValidationException()
    {
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns("{}"));
        await Assert.ThrowsAsync<ImageValidationException>(
            () => extractor.ExtractTraitsAsync(Array.Empty<FashionImageInput>()));
    }

    // 2. Unsupported MIME type
    [Fact]
    public async Task ExtractTraitsAsync_UnsupportedMimeType_ThrowsImageValidationException()
    {
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns("{}"));
        var images = new[] { TestHelpers.Image("img_1", "application/pdf") };
        await Assert.ThrowsAsync<ImageValidationException>(
            () => extractor.ExtractTraitsAsync(images));
    }

    // 3. Empty image bytes
    [Fact]
    public async Task ExtractTraitsAsync_EmptyImageBytes_ThrowsImageValidationException()
    {
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns("{}"));
        var images = new[] { new FashionImageInput("img_1", Array.Empty<byte>(), "image/jpeg") };
        await Assert.ThrowsAsync<ImageValidationException>(
            () => extractor.ExtractTraitsAsync(images));
    }

    // 4. Successful single-image extraction
    [Fact]
    public async Task ExtractTraitsAsync_SingleImage_ReturnsParsedResult()
    {
        const string json = """
            {
              "images": [{
                "imageId": "photo_1",
                "people": [{
                  "personId": "person_1",
                  "position": "center",
                  "overallStyle": ["casual"],
                  "styleTags": ["casual", "minimalist"],
                  "clothingItems": [{
                    "category": "top",
                    "type": "t-shirt",
                    "subtype": null,
                    "primaryColor": { "normalized": "blue", "shade": "navy blue" },
                    "secondaryColors": [],
                    "pattern": "solid",
                    "material": "cotton-like",
                    "fit": "regular",
                    "length": null,
                    "sleeveLength": "short",
                    "neckline": "round",
                    "collarType": null,
                    "waistRise": null,
                    "closureType": null,
                    "details": [],
                    "visibleText": null,
                    "brand": null,
                    "logo": null,
                    "condition": "good",
                    "styleTags": ["casual"],
                    "confidence": 0.92
                  }],
                  "overallConfidence": 0.90
                }],
                "warnings": []
              }]
            }
            """;

        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns(json));
        var result = await extractor.ExtractTraitsAsync(new[] { TestHelpers.Image("photo_1") });

        Assert.Single(result.Images);
        var image = result.Images[0];
        Assert.Equal("photo_1", image.ImageId);
        Assert.Single(image.People);

        var person = image.People[0];
        Assert.Equal("person_1", person.PersonId);
        Assert.Equal("center", person.Position);
        Assert.Equal(0.90, person.OverallConfidence);

        var item = person.ClothingItems[0];
        Assert.Equal("top", item.Category);
        Assert.Equal("t-shirt", item.Type);
        Assert.NotNull(item.PrimaryColor);
        Assert.Equal("blue", item.PrimaryColor!.Normalized);
        Assert.Equal("navy blue", item.PrimaryColor.Shade);
        Assert.Equal("solid", item.Pattern);
        Assert.Equal(0.92, item.Confidence);
    }

    // 5. Successful multi-image extraction
    [Fact]
    public async Task ExtractTraitsAsync_MultipleImages_ReturnsResultForEach()
    {
        const string json = """
            {
              "images": [
                {
                  "imageId": "img_a",
                  "people": [{ "personId": "person_1", "position": "left", "overallStyle": [], "styleTags": [], "clothingItems": [], "overallConfidence": 0.80 }],
                  "warnings": []
                },
                {
                  "imageId": "img_b",
                  "people": [{ "personId": "person_1", "position": "right", "overallStyle": [], "styleTags": [], "clothingItems": [], "overallConfidence": 0.85 }],
                  "warnings": []
                }
              ]
            }
            """;

        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns(json));
        var images = new[] { TestHelpers.Image("img_a"), TestHelpers.Image("img_b", "image/png") };
        var result = await extractor.ExtractTraitsAsync(images);

        Assert.Equal(2, result.Images.Count);
        Assert.Equal("img_a", result.Images[0].ImageId);
        Assert.Equal("img_b", result.Images[1].ImageId);
        Assert.Single(result.Images[0].People);
        Assert.Single(result.Images[1].People);
    }

    // 6. Multiple people in one image
    [Fact]
    public async Task ExtractTraitsAsync_MultiplePeople_ReturnsAllPeople()
    {
        const string json = """
            {
              "images": [{
                "imageId": "group",
                "people": [
                  { "personId": "person_1", "position": "left",  "overallStyle": [], "styleTags": [], "clothingItems": [], "overallConfidence": 0.88 },
                  { "personId": "person_2", "position": "right", "overallStyle": [], "styleTags": [], "clothingItems": [], "overallConfidence": 0.82 }
                ],
                "warnings": []
              }]
            }
            """;

        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns(json));
        var result = await extractor.ExtractTraitsAsync(new[] { TestHelpers.Image("group") });

        Assert.Equal(2, result.Images[0].People.Count);
        Assert.Equal("person_1", result.Images[0].People[0].PersonId);
        Assert.Equal("person_2", result.Images[0].People[1].PersonId);
    }

    // 7. Malformed JSON response
    [Fact]
    public async Task ExtractTraitsAsync_MalformedJson_ThrowsTraitsParseException()
    {
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns("not-valid-json{{{"));
        await Assert.ThrowsAsync<TraitsParseException>(
            () => extractor.ExtractTraitsAsync(new[] { TestHelpers.Image("img_1") }));
    }

    // 8. Empty model response
    [Fact]
    public async Task ExtractTraitsAsync_EmptyModelResponse_ThrowsExtractionException()
    {
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.ReturnsEmpty());
        await Assert.ThrowsAsync<ExtractionException>(
            () => extractor.ExtractTraitsAsync(new[] { TestHelpers.Image("img_1") }));
    }

    // 9. Cancellation — pre-cancelled token propagates as OperationCanceledException
    [Fact]
    public async Task ExtractTraitsAsync_PreCancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns("{}"));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => extractor.ExtractTraitsAsync(new[] { TestHelpers.Image("img_1") }, cts.Token));
    }

    // 10. Transient adapter failure is wrapped in ExtractionException
    [Fact]
    public async Task ExtractTraitsAsync_AdapterThrows_WrapsInExtractionException()
    {
        var extractor = TestHelpers.Create(
            FakeAzureOpenAIChatAdapter.Throws(new InvalidOperationException("network error")));
        var ex = await Assert.ThrowsAsync<ExtractionException>(
            () => extractor.ExtractTraitsAsync(new[] { TestHelpers.Image("img_1") }));
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    // 11. Content filtering
    [Fact]
    public async Task ExtractTraitsAsync_ContentFiltered_ThrowsContentFilterException()
    {
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.ReturnsFiltered());
        await Assert.ThrowsAsync<ContentFilterException>(
            () => extractor.ExtractTraitsAsync(new[] { TestHelpers.Image("img_1") }));
    }

    // 12. Unknown / null clothing attributes are handled without exception
    [Fact]
    public async Task ExtractTraitsAsync_NullAttributes_ReturnsUnknownDefaults()
    {
        const string json = """
            {
              "images": [{
                "imageId": "img_1",
                "people": [{
                  "personId": "person_1",
                  "position": null,
                  "overallStyle": [],
                  "styleTags": [],
                  "clothingItems": [{
                    "category": "unknown",
                    "type": "unknown",
                    "subtype": null,
                    "primaryColor": null,
                    "secondaryColors": [],
                    "pattern": "unknown",
                    "material": null,
                    "fit": null,
                    "length": null,
                    "sleeveLength": null,
                    "neckline": null,
                    "collarType": null,
                    "waistRise": null,
                    "closureType": null,
                    "details": [],
                    "visibleText": null,
                    "brand": null,
                    "logo": null,
                    "condition": null,
                    "styleTags": [],
                    "confidence": 0.45
                  }],
                  "overallConfidence": 0.50
                }],
                "warnings": ["Low confidence detection"]
              }]
            }
            """;

        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns(json));
        var result = await extractor.ExtractTraitsAsync(new[] { TestHelpers.Image("img_1") });

        var item = result.Images[0].People[0].ClothingItems[0];
        Assert.Equal("unknown", item.Category);
        Assert.Null(item.PrimaryColor);
        Assert.Empty(item.SecondaryColors);
        Assert.Equal(0.45, item.Confidence);
        Assert.Equal("Low confidence detection", result.Images[0].Warnings[0]);
    }

    // 13. Indian clothing — saree + blouse schema
    [Fact]
    public async Task ExtractTraitsAsync_IndianClothing_ParsesFullTaxonomy()
    {
        const string json = """
            {
              "images": [{
                "imageId": "saree_photo",
                "people": [{
                  "personId": "person_1",
                  "position": "center",
                  "overallStyle": ["traditional", "festive"],
                  "styleTags": ["ethnic", "festive"],
                  "clothingItems": [
                    {
                      "category": "traditional-wear",
                      "type": "saree",
                      "subtype": "silk saree",
                      "primaryColor": { "normalized": "red", "shade": "deep red" },
                      "secondaryColors": [{ "normalized": "gold", "shade": "golden zari" }],
                      "pattern": "embroidered",
                      "material": "silk-like",
                      "fit": null,
                      "length": "full-length",
                      "sleeveLength": null,
                      "neckline": null,
                      "collarType": null,
                      "waistRise": null,
                      "closureType": null,
                      "details": ["zari border", "pallu with motifs", "temple border design"],
                      "visibleText": null,
                      "brand": null,
                      "logo": null,
                      "condition": "excellent",
                      "styleTags": ["ethnic", "festive", "traditional"],
                      "confidence": 0.93
                    },
                    {
                      "category": "top",
                      "type": "blouse",
                      "subtype": "saree blouse",
                      "primaryColor": { "normalized": "red", "shade": "matching red" },
                      "secondaryColors": [{ "normalized": "gold", "shade": "golden border" }],
                      "pattern": "embroidered",
                      "material": "silk-like",
                      "fit": "regular",
                      "length": "cropped",
                      "sleeveLength": "short",
                      "neckline": "round",
                      "collarType": null,
                      "waistRise": null,
                      "closureType": "hook",
                      "details": ["back hooks", "gold border trim"],
                      "visibleText": null,
                      "brand": null,
                      "logo": null,
                      "condition": "excellent",
                      "styleTags": ["ethnic", "traditional"],
                      "confidence": 0.89
                    }
                  ],
                  "overallConfidence": 0.91
                }],
                "warnings": []
              }]
            }
            """;

        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns(json));
        var result = await extractor.ExtractTraitsAsync(new[] { TestHelpers.Image("saree_photo") });

        var person = result.Images[0].People[0];
        Assert.Equal(2, person.ClothingItems.Count);
        Assert.Contains("ethnic", person.StyleTags);

        var saree = person.ClothingItems[0];
        Assert.Equal("traditional-wear", saree.Category);
        Assert.Equal("saree", saree.Type);
        Assert.Equal("silk-like", saree.Material);
        Assert.Equal("embroidered", saree.Pattern);
        Assert.Equal("full-length", saree.Length);
        Assert.Contains("zari border", saree.Details);
        Assert.Contains("pallu with motifs", saree.Details);
        Assert.Equal("red", saree.PrimaryColor?.Normalized);
        Assert.Equal("deep red", saree.PrimaryColor?.Shade);
        Assert.Single(saree.SecondaryColors);
        Assert.Equal("gold", saree.SecondaryColors[0].Normalized);

        var blouse = person.ClothingItems[1];
        Assert.Equal("top", blouse.Category);
        Assert.Equal("blouse", blouse.Type);
        Assert.Equal("hook", blouse.ClosureType);
        Assert.Equal("short", blouse.SleeveLength);
        Assert.Equal("round", blouse.Neckline);
    }

    // 14. Image ID mapping — model returns images out of request order
    [Fact]
    public async Task ExtractTraitsAsync_ModelReturnsOutOfOrder_ResultsMappedToRequestOrder()
    {
        // Model returns second_image before first_image
        const string json = """
            {
              "images": [
                {
                  "imageId": "second_image",
                  "people": [{ "personId": "person_1", "position": "center", "overallStyle": [], "styleTags": [], "clothingItems": [], "overallConfidence": 0.80 }],
                  "warnings": []
                },
                {
                  "imageId": "first_image",
                  "people": [{ "personId": "person_1", "position": "left", "overallStyle": [], "styleTags": [], "clothingItems": [], "overallConfidence": 0.85 }],
                  "warnings": []
                }
              ]
            }
            """;

        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns(json));
        // Request: first_image, then second_image
        var images = new[]
        {
            TestHelpers.Image("first_image"),
            TestHelpers.Image("second_image"),
        };
        var result = await extractor.ExtractTraitsAsync(images);

        // Result should follow request order, not model response order
        Assert.Equal("first_image", result.Images[0].ImageId);
        Assert.Equal("second_image", result.Images[1].ImageId);

        // Person positions must map to the correct image
        Assert.Equal("left", result.Images[0].People[0].Position);
        Assert.Equal("center", result.Images[1].People[0].Position);
    }

    // Extra: too many images
    [Fact]
    public async Task ExtractTraitsAsync_ExceedsMaxImages_ThrowsImageValidationException()
    {
        var opts = new AzureOpenAIOptions
        {
            MaxImages = 2,
            MaxImageSizeBytes = 5L * 1024 * 1024,
        };
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns("{}"), opts);
        var images = new[]
        {
            TestHelpers.Image("img_1"),
            TestHelpers.Image("img_2"),
            TestHelpers.Image("img_3"),
        };
        await Assert.ThrowsAsync<ImageValidationException>(
            () => extractor.ExtractTraitsAsync(images));
    }

    // Extra: image exceeds size limit
    [Fact]
    public async Task ExtractTraitsAsync_ImageExceedsSizeLimit_ThrowsImageValidationException()
    {
        var opts = new AzureOpenAIOptions
        {
            MaxImages = 5,
            MaxImageSizeBytes = 100,
        };
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns("{}"), opts);
        var images = new[] { new FashionImageInput("img_1", new byte[200], "image/jpeg") };
        await Assert.ThrowsAsync<ImageValidationException>(
            () => extractor.ExtractTraitsAsync(images));
    }

    // Extra: duplicate ImageId
    [Fact]
    public async Task ExtractTraitsAsync_DuplicateImageId_ThrowsImageValidationException()
    {
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns("{}"));
        var images = new[]
        {
            TestHelpers.Image("same_id"),
            TestHelpers.Image("same_id"),
        };
        await Assert.ThrowsAsync<ImageValidationException>(
            () => extractor.ExtractTraitsAsync(images));
    }

    // Extra: model omits one image — result still contains entry with warning
    [Fact]
    public async Task ExtractTraitsAsync_ModelOmitsImage_ResultContainsWarningEntry()
    {
        const string json = """
            {
              "images": [
                {
                  "imageId": "img_a",
                  "people": [],
                  "warnings": []
                }
              ]
            }
            """;
        // We request both img_a and img_b but model only returns img_a
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns(json));
        var images = new[] { TestHelpers.Image("img_a"), TestHelpers.Image("img_b") };
        var result = await extractor.ExtractTraitsAsync(images);

        Assert.Equal(2, result.Images.Count);
        var missing = result.Images.First(i => i.ImageId == "img_b");
        Assert.Empty(missing.People);
        Assert.NotEmpty(missing.Warnings);
    }

    // Extra: code-fenced JSON is stripped and parsed successfully
    [Fact]
    public async Task ExtractTraitsAsync_CodeFencedResponse_StrippedAndParsedSuccessfully()
    {
        var fencedJson = "```json\n{\"images\":[{\"imageId\":\"img_1\",\"people\":[],\"warnings\":[]}]}\n```";
        var extractor = TestHelpers.Create(FakeAzureOpenAIChatAdapter.Returns(fencedJson));
        var result = await extractor.ExtractTraitsAsync(new[] { TestHelpers.Image("img_1") });

        Assert.Single(result.Images);
        Assert.Equal("img_1", result.Images[0].ImageId);
    }
}
