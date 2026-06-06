using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Robe.Core.Domain;
using Robe.Core.Interfaces;
using Robe.Infrastructure.Auth;
using Robe.Infrastructure.Persistence;
using Robe.Infrastructure.Secrets;
using Robe.Infrastructure.Storage;
using Robe.Infrastructure.TraitsExtraction;

namespace Robe.Api.Tests;

public class GarmentStorageTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public GarmentStorageTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ── Setup helpers ────────────────────────────────────────────────────────

    private (HttpClient client, InMemoryGarmentRepository repo, InMemoryImageStore imageStore) CreateSetup()
    {
        var repo       = new InMemoryGarmentRepository();
        var imageStore = new InMemoryImageStore();

        var client = _factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITraitsExtractor>();
                services.AddScoped<ITraitsExtractor>(_ => FakeTraitsExtractor.ReturnsDefault());

                services.RemoveAll<ISecretManager>();
                services.AddSingleton<ISecretManager>(new MockSecretManager(new Dictionary<string, string>()));

                services.RemoveAll<IGarmentRepository>();
                services.AddSingleton<IGarmentRepository>(repo);

                services.RemoveAll<IImageStore>();
                services.AddSingleton<IImageStore>(imageStore);

                services.RemoveAll<ICurrentUser>();
                services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
                // LocalAuthHandler is already registered by Program.cs — no need to re-add it here.
            });
        }).CreateClient();

        return (client, repo, imageStore);
    }

    private HttpContent GarmentBody(GarmentTraits traits, string mimeType = "image/jpeg") =>
        new StringContent(
            JsonSerializer.Serialize(
                new { traits, imageBase64 = Convert.ToBase64String(new byte[512]), mimeType },
                JsonOpts),
            Encoding.UTF8, "application/json");

    private static HttpRequestMessage AuthedPost(string url, HttpContent body, string userId = "user-a")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = body };
        req.Headers.Add(LocalAuthHandler.UserIdHeader, userId);
        return req;
    }

    private static HttpRequestMessage AuthedGet(string url, string userId = "user-a")
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add(LocalAuthHandler.UserIdHeader, userId);
        return req;
    }

    private static HttpRequestMessage AuthedDelete(string url, string userId = "user-a")
    {
        var req = new HttpRequestMessage(HttpMethod.Delete, url);
        req.Headers.Add(LocalAuthHandler.UserIdHeader, userId);
        return req;
    }

    private async Task<JsonElement> PostGarmentAsync(HttpClient client, GarmentTraits traits, string userId = "user-a")
    {
        var resp = await client.SendAsync(AuthedPost("/api/garments", GarmentBody(traits), userId));
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task StoreAndRetrieve_RoundTripsTraitsIntact()
    {
        var (client, _, _) = CreateSetup();
        var traits = FakeTraitsExtractor.BuildDefaultTraits();

        var created = await PostGarmentAsync(client, traits);
        var id      = created.GetProperty("id").GetString()!;

        var getResp = await client.SendAsync(AuthedGet($"/api/garments/{id}"));
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var body = await getResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(id, body.GetProperty("id").GetString());
        Assert.Equal("top", body.GetProperty("traits").GetProperty("category").GetString());
        Assert.Equal("t-shirt", body.GetProperty("traits").GetProperty("subcategory").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("imageUrl").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("createdAt").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("modifiedAt").GetString()));
        Assert.Equal("user-a", body.GetProperty("createdByUserId").GetString());
        Assert.Equal("user-a", body.GetProperty("modifiedByUserId").GetString());
    }

    [Fact]
    public async Task List_ReturnsOnlyCallerItems()
    {
        var (client, _, _) = CreateSetup();
        var traits = FakeTraitsExtractor.BuildDefaultTraits();

        await PostGarmentAsync(client, traits, "user-a");
        await PostGarmentAsync(client, traits, "user-a");
        await PostGarmentAsync(client, traits, "user-b");

        var respA = await client.SendAsync(AuthedGet("/api/garments", "user-a"));
        var bodyA = await respA.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(2, bodyA.GetProperty("items").GetArrayLength());

        var respB = await client.SendAsync(AuthedGet("/api/garments", "user-b"));
        var bodyB = await respB.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(1, bodyB.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task List_CategoryFilterReturnsMatchingItemsOnly()
    {
        var (client, _, _) = CreateSetup();

        var top    = FakeTraitsExtractor.BuildDefaultTraits(); // category = top
        var bottom = new GarmentTraits
        {
            Category         = GarmentCategory.Bottom,
            Subcategory      = "jeans",
            PrimaryColor     = new ColorInfo("blue", "#0000ff"),
            SecondaryColors  = new List<ColorInfo>(),
            Pattern          = GarmentPattern.Solid,
            Formality        = 1,
            Seasonality      = new List<Season> { Season.All },
            StyleTags        = new List<string> { "casual" },
            Occasions        = new List<string> { "everyday" },
            Confidence       = 0.9
        };

        await PostGarmentAsync(client, top);
        await PostGarmentAsync(client, top);
        await PostGarmentAsync(client, bottom);

        var resp = await client.SendAsync(AuthedGet("/api/garments?category=top"));
        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetById_UserBCannotReadUserAGarment_Returns404()
    {
        var (client, _, _) = CreateSetup();
        var traits  = FakeTraitsExtractor.BuildDefaultTraits();
        var created = await PostGarmentAsync(client, traits, "user-a");
        var id      = created.GetProperty("id").GetString()!;

        var resp = await client.SendAsync(AuthedGet($"/api/garments/{id}", "user-b"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Delete_UserBCannotDeleteUserAGarment_Returns404()
    {
        var (client, _, _) = CreateSetup();
        var traits  = FakeTraitsExtractor.BuildDefaultTraits();
        var created = await PostGarmentAsync(client, traits, "user-a");
        var id      = created.GetProperty("id").GetString()!;

        var resp = await client.SendAsync(AuthedDelete($"/api/garments/{id}", "user-b"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task NoAuthHeader_Returns401ForAllEndpoints()
    {
        var (client, _, _) = CreateSetup();
        var body = GarmentBody(FakeTraitsExtractor.BuildDefaultTraits());

        var post   = await client.PostAsync("/api/garments", body);
        var list   = await client.GetAsync("/api/garments");
        var getOne = await client.GetAsync("/api/garments/grm_any");
        var delete = await client.DeleteAsync("/api/garments/grm_any");

        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, getOne.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, delete.StatusCode);
    }

    [Fact]
    public async Task Delete_RemovesBothDbRowAndBlob()
    {
        var (client, _, imageStore) = CreateSetup();
        var traits  = FakeTraitsExtractor.BuildDefaultTraits();
        var created = await PostGarmentAsync(client, traits);
        var id      = created.GetProperty("id").GetString()!;
        var blobKey = $"{id}.jpg";

        Assert.True(imageStore.HasKey(blobKey), "Blob should exist after create.");

        var del = await client.SendAsync(AuthedDelete($"/api/garments/{id}"));
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var getAfter = await client.SendAsync(AuthedGet($"/api/garments/{id}"));
        Assert.Equal(HttpStatusCode.NotFound, getAfter.StatusCode);

        Assert.False(imageStore.HasKey(blobKey), "Blob should be removed after delete.");
    }

    [Fact]
    public async Task GetById_NonExistentId_Returns404()
    {
        var (client, _, _) = CreateSetup();
        var resp = await client.SendAsync(AuthedGet("/api/garments/grm_doesnotexist"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task List_Paging_ReturnsCorrectPage()
    {
        var (client, _, _) = CreateSetup();
        var traits = FakeTraitsExtractor.BuildDefaultTraits();

        for (var i = 0; i < 5; i++)
            await PostGarmentAsync(client, traits);

        var resp = await client.SendAsync(AuthedGet("/api/garments?page=2&pageSize=2"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
        Assert.Equal(2, body.GetProperty("page").GetInt32());
        Assert.Equal(2, body.GetProperty("pageSize").GetInt32());
    }
}
