using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Robe.Core.Exceptions;
using Robe.Core.Interfaces;
using Robe.Infrastructure.Secrets;
using Robe.Infrastructure.TraitsExtraction;

namespace Robe.Api.Tests;

public class GarmentAnalyzeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public GarmentAnalyzeTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient BuildClient(ITraitsExtractor extractor)
    {
        return _factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITraitsExtractor>();
                services.AddScoped(_ => extractor);
                services.RemoveAll<ISecretManager>();
                services.AddSingleton<ISecretManager>(new MockSecretManager(new Dictionary<string, string>()));
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Analyze_ValidImage_Returns200WithWellFormedTraits()
    {
        var client = BuildClient(FakeTraitsExtractor.ReturnsDefault());
        var body = new { imageBase64 = SmallBase64(), mimeType = "image/jpeg" };

        var response = await client.PostAsJsonAsync("/api/garments/analyze", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("azure-openai-gpt-5-mini", json.GetProperty("modelVersion").GetString());

        var traits = json.GetProperty("traits");
        Assert.Equal("top", traits.GetProperty("category").GetString());
        Assert.False(string.IsNullOrEmpty(traits.GetProperty("subcategory").GetString()));
        Assert.True(traits.GetProperty("confidence").GetDouble() > 0);
        Assert.NotNull(traits.GetProperty("primaryColor").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Analyze_NoGarmentDetected_Returns422()
    {
        var client = BuildClient(FakeTraitsExtractor.Throws(new GarmentNotDetectedException()));
        var body = new { imageBase64 = SmallBase64(), mimeType = "image/jpeg" };

        var response = await client.PostAsJsonAsync("/api/garments/analyze", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_MissingImageBase64_Returns400()
    {
        var client = BuildClient(FakeTraitsExtractor.ReturnsDefault());
        var body = new { imageBase64 = "", mimeType = "image/jpeg" };

        var response = await client.PostAsJsonAsync("/api/garments/analyze", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_OversizedImage_Returns400()
    {
        var client = BuildClient(FakeTraitsExtractor.ReturnsDefault());
        var oversized = Convert.ToBase64String(new byte[11 * 1024 * 1024]);
        var body = new { imageBase64 = oversized, mimeType = "image/jpeg" };

        var response = await client.PostAsJsonAsync("/api/garments/analyze", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_UnsupportedMimeType_Returns400()
    {
        var client = BuildClient(FakeTraitsExtractor.ReturnsDefault());
        var body = new { imageBase64 = SmallBase64(), mimeType = "application/pdf" };

        var response = await client.PostAsJsonAsync("/api/garments/analyze", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Analyze_TraitsParseException_Returns502()
    {
        var client = BuildClient(FakeTraitsExtractor.Throws(new TraitsParseException("bad json")));
        var body = new { imageBase64 = SmallBase64(), mimeType = "image/jpeg" };

        var response = await client.PostAsJsonAsync("/api/garments/analyze", body);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("error", out _), "Response should contain an 'error' field, not a raw stack trace.");
    }

    [Fact]
    public async Task Analyze_ExtractionException_Returns502()
    {
        var client = BuildClient(FakeTraitsExtractor.Throws(new ExtractionException("call failed")));
        var body = new { imageBase64 = SmallBase64(), mimeType = "image/jpeg" };

        var response = await client.PostAsJsonAsync("/api/garments/analyze", body);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(json.TryGetProperty("error", out _), "Response should contain an 'error' field, not a raw stack trace.");
    }

    private static string SmallBase64() => Convert.ToBase64String(new byte[512]);
}
