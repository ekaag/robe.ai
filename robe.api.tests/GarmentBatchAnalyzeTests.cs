using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Robe.Core.Domain;
using Robe.Core.Exceptions;
using Robe.Core.Interfaces;
using Robe.Infrastructure.Secrets;
using Robe.Infrastructure.TraitsExtraction;

namespace Robe.Api.Tests;

public class GarmentBatchAnalyzeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GarmentBatchAnalyzeTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient BuildClient(IFashionTraitsExtractor extractor)
    {
        return _factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<IFashionTraitsExtractor>();
                services.AddSingleton(_ => extractor);
                services.RemoveAll<ISecretManager>();
                services.AddSingleton<ISecretManager>(new MockSecretManager(new Dictionary<string, string>()));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task AnalyzeBatch_ValidImages_Returns200WithResultsPerImage()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.ReturnsDefault());
        var body = new
        {
            images = new[]
            {
                new { imageId = "img-a", imageBase64 = SmallBase64(), mimeType = "image/jpeg" },
                new { imageId = "img-b", imageBase64 = SmallBase64(), mimeType = "image/png" }
            }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("azure-openai-gpt-5-mini", json.GetProperty("modelVersion").GetString());
        var images = json.GetProperty("images");
        Assert.Equal(2, images.GetArrayLength());
        Assert.Equal("img-a", images[0].GetProperty("imageId").GetString());
        Assert.Equal("img-b", images[1].GetProperty("imageId").GetString());
    }

    [Fact]
    public async Task AnalyzeBatch_ResultsContainPeopleAndClothingItems()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.ReturnsDefault());
        var body = new
        {
            images = new[] { new { imageId = "img-1", imageBase64 = SmallBase64(), mimeType = "image/jpeg" } }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var person = json.GetProperty("images")[0].GetProperty("people")[0];
        Assert.Equal("person-1", person.GetProperty("personId").GetString());
        Assert.True(person.GetProperty("overallConfidence").GetDouble() > 0);
        Assert.True(person.GetProperty("clothingItems").GetArrayLength() > 0);
    }

    [Fact]
    public async Task AnalyzeBatch_ResultsContainFaceAndGarmentBoundingBoxes()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.ReturnsDefault());
        var body = new
        {
            images = new[] { new { imageId = "img-1", imageBase64 = SmallBase64(), mimeType = "image/jpeg" } }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var person = json.GetProperty("images")[0].GetProperty("people")[0];

        var face = person.GetProperty("faceBoundingBox");
        AssertNormalizedBox(face);

        var item = person.GetProperty("clothingItems")[0];
        var box = item.GetProperty("boundingBox");
        AssertNormalizedBox(box);
    }

    [Fact]
    public async Task AnalyzeBatch_OmittedBoundingBoxes_ReturnNullNotError()
    {
        var expected = new TraitsExtractionResult(new List<ImageTraitsResult>
        {
            new ImageTraitsResult("img-1", new List<PersonTraits>
            {
                new PersonTraits(
                    PersonId: "person-1",
                    Position: "center",
                    OverallStyle: new List<string>(),
                    StyleTags: new List<string>(),
                    ClothingItems: new List<ClothingItemTraits>
                    {
                        new ClothingItemTraits(
                            Category: "top", Type: "t-shirt", Subtype: null,
                            PrimaryColor: null, SecondaryColors: new List<ColorTraits>(),
                            Pattern: null, Material: null, Fit: null, Length: null,
                            SleeveLength: null, Neckline: null, CollarType: null,
                            WaistRise: null, ClosureType: null, Details: new List<string>(),
                            VisibleText: null, Brand: null, Logo: null, Condition: null,
                            StyleTags: new List<string>(), Confidence: 0.5,
                            BoundingBox: null)
                    },
                    OverallConfidence: 0.5,
                    FaceBoundingBox: null)
            }, new List<string>())
        });
        var client = BuildClient(FakeFashionTraitsExtractor.Returns(_ => expected));
        var body = new
        {
            images = new[] { new { imageId = "img-1", imageBase64 = SmallBase64(), mimeType = "image/jpeg" } }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var person = json.GetProperty("images")[0].GetProperty("people")[0];
        Assert.Equal(JsonValueKind.Null, person.GetProperty("faceBoundingBox").ValueKind);
        Assert.Equal(JsonValueKind.Null, person.GetProperty("clothingItems")[0].GetProperty("boundingBox").ValueKind);
    }

    private static void AssertNormalizedBox(JsonElement box)
    {
        Assert.InRange(box.GetProperty("x").GetDouble(), 0.0, 1.0);
        Assert.InRange(box.GetProperty("y").GetDouble(), 0.0, 1.0);
        Assert.InRange(box.GetProperty("width").GetDouble(), 0.0, 1.0);
        Assert.InRange(box.GetProperty("height").GetDouble(), 0.0, 1.0);
    }

    [Fact]
    public async Task AnalyzeBatch_AutoAssignsImageIdWhenNotProvided()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.ReturnsDefault());
        var body = new
        {
            images = new[]
            {
                new { imageBase64 = SmallBase64(), mimeType = "image/jpeg" },
                new { imageBase64 = SmallBase64(), mimeType = "image/jpeg" }
            }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("img-1", json.GetProperty("images")[0].GetProperty("imageId").GetString());
        Assert.Equal("img-2", json.GetProperty("images")[1].GetProperty("imageId").GetString());
    }

    [Fact]
    public async Task AnalyzeBatch_EmptyImagesList_Returns400()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.ReturnsDefault());
        var body = new { images = Array.Empty<object>() };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeBatch_MissingImagesField_Returns400()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.ReturnsDefault());
        var body = new { };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeBatch_TooManyImages_Returns400()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.ReturnsDefault());
        var body = new
        {
            images = Enumerable.Range(1, 11)
                .Select(i => new { imageId = $"img-{i}", imageBase64 = SmallBase64(), mimeType = "image/jpeg" })
                .ToArray()
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeBatch_InvalidBase64_Returns400()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.ReturnsDefault());
        var body = new
        {
            images = new[] { new { imageId = "img-1", imageBase64 = "!!!not-base64!!!", mimeType = "image/jpeg" } }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeBatch_OversizedImage_Returns400()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.ReturnsDefault());
        var body = new
        {
            images = new[] { new { imageId = "img-1", imageBase64 = Convert.ToBase64String(new byte[11 * 1024 * 1024]), mimeType = "image/jpeg" } }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeBatch_UnsupportedMimeType_Returns400()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.ReturnsDefault());
        var body = new
        {
            images = new[] { new { imageId = "img-1", imageBase64 = SmallBase64(), mimeType = "application/pdf" } }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeBatch_ExtractionException_Returns502()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.Throws(new ExtractionException("model unavailable")));
        var body = new
        {
            images = new[] { new { imageId = "img-1", imageBase64 = SmallBase64(), mimeType = "image/jpeg" } }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task AnalyzeBatch_TraitsParseException_Returns502()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.Throws(new TraitsParseException("bad json")));
        var body = new
        {
            images = new[] { new { imageId = "img-1", imageBase64 = SmallBase64(), mimeType = "image/jpeg" } }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task AnalyzeBatch_ContentFilterException_Returns502()
    {
        var client = BuildClient(FakeFashionTraitsExtractor.Throws(new ContentFilterException()));
        var body = new
        {
            images = new[] { new { imageId = "img-1", imageBase64 = SmallBase64(), mimeType = "image/jpeg" } }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task AnalyzeBatch_CustomResultFactory_ReturnsExpectedStructure()
    {
        var expected = new TraitsExtractionResult(new List<ImageTraitsResult>
        {
            new ImageTraitsResult("img-custom", new List<PersonTraits>(), new List<string> { "low quality" })
        });
        var client = BuildClient(FakeFashionTraitsExtractor.Returns(_ => expected));
        var body = new
        {
            images = new[] { new { imageId = "img-custom", imageBase64 = SmallBase64(), mimeType = "image/jpeg" } }
        };

        var response = await client.PostAsJsonAsync("/api/garments/analyze-batch", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var img = json.GetProperty("images")[0];
        Assert.Equal("img-custom", img.GetProperty("imageId").GetString());
        Assert.Equal(0, img.GetProperty("people").GetArrayLength());
        Assert.Equal("low quality", img.GetProperty("warnings")[0].GetString());
    }

    private static string SmallBase64() => Convert.ToBase64String(new byte[512]);
}
