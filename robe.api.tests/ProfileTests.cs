using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Robe.Core.Domain;
using Robe.Core.Interfaces;
using Robe.Infrastructure.Auth;
using Robe.Infrastructure.Persistence;
using Robe.Infrastructure.Profile;
using Robe.Infrastructure.Secrets;
using Robe.Infrastructure.Storage;
using Robe.Infrastructure.TraitsExtraction;

namespace Robe.Api.Tests;

public class ProfileTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public ProfileTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ── Setup helpers ────────────────────────────────────────────────────────

    private (HttpClient client, InMemoryGarmentRepository garmentRepo, InMemoryProfileRepository profileRepo) CreateSetup()
    {
        var garmentRepo = new InMemoryGarmentRepository();
        var profileRepo = new InMemoryProfileRepository();

        var client = _factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITraitsExtractor>();
                services.AddScoped<ITraitsExtractor>(_ => FakeTraitsExtractor.ReturnsDefault());

                services.RemoveAll<ISecretManager>();
                services.AddSingleton<ISecretManager>(new MockSecretManager(new Dictionary<string, string>()));

                services.RemoveAll<IGarmentRepository>();
                services.AddSingleton<IGarmentRepository>(garmentRepo);

                services.RemoveAll<IImageStore>();
                services.AddSingleton<IImageStore>(new InMemoryImageStore());

                services.RemoveAll<IProfileGenerator>();
                services.AddScoped<IProfileGenerator, FakeProfileGenerator>();

                services.RemoveAll<IProfileRepository>();
                services.AddSingleton<IProfileRepository>(profileRepo);

                services.RemoveAll<ICurrentUser>();
                services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
            });
        }).CreateClient();

        return (client, garmentRepo, profileRepo);
    }

    private HttpContent GarmentBody(GarmentTraits traits, string mimeType = "image/jpeg") =>
        new StringContent(
            JsonSerializer.Serialize(
                new { traits, imageBase64 = Convert.ToBase64String(new byte[512]), mimeType },
                JsonOpts),
            Encoding.UTF8, "application/json");

    private static HttpRequestMessage AuthedPost(string url, HttpContent? body, string userId = "user-a")
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

    private async Task SeedGarmentAsync(HttpClient client, GarmentTraits traits, string userId = "user-a")
    {
        var resp = await client.SendAsync(AuthedPost("/api/garments", GarmentBody(traits), userId));
        resp.EnsureSuccessStatusCode();
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Generate_WithGarments_ReturnsProfileReflectingTraitDistribution()
    {
        var (client, _, _) = CreateSetup();
        var traits = FakeTraitsExtractor.BuildDefaultTraits(); // minimalist + casual, top

        await SeedGarmentAsync(client, traits);
        await SeedGarmentAsync(client, traits);

        var resp = await client.SendAsync(AuthedPost("/api/users/me/profile/generate", null));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(2, body.GetProperty("garmentCount").GetInt32());

        var styles = body.GetProperty("dominantStyles").EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("minimalist", styles);

        Assert.False(string.IsNullOrEmpty(body.GetProperty("createdAt").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("modifiedAt").GetString()));
        Assert.Equal("user-a", body.GetProperty("createdByUserId").GetString());
        Assert.Equal("user-a", body.GetProperty("modifiedByUserId").GetString());
    }

    [Fact]
    public async Task Generate_ZeroGarments_ReturnsEmptyProfile_Not500()
    {
        var (client, _, _) = CreateSetup();

        var resp = await client.SendAsync(AuthedPost("/api/users/me/profile/generate", null));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(0, body.GetProperty("garmentCount").GetInt32());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("summary").GetString()));
    }

    [Fact]
    public async Task Get_BeforeGenerate_Returns404()
    {
        var (client, _, _) = CreateSetup();
        var resp = await client.SendAsync(AuthedGet("/api/users/me/profile"));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Get_AfterGenerate_ReturnsSavedProfile()
    {
        var (client, _, _) = CreateSetup();
        var traits = FakeTraitsExtractor.BuildDefaultTraits();
        await SeedGarmentAsync(client, traits);

        var genResp = await client.SendAsync(AuthedPost("/api/users/me/profile/generate", null));
        genResp.EnsureSuccessStatusCode();
        var generated = await genResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        var getResp = await client.SendAsync(AuthedGet("/api/users/me/profile"));
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var fetched = await getResp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(
            generated.GetProperty("garmentCount").GetInt32(),
            fetched.GetProperty("garmentCount").GetInt32());
        Assert.Equal(
            generated.GetProperty("modifiedAt").GetString(),
            fetched.GetProperty("modifiedAt").GetString());
    }

    [Fact]
    public async Task Generate_NoAuth_Returns401()
    {
        var (client, _, _) = CreateSetup();
        var resp = await client.PostAsync("/api/users/me/profile/generate", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Get_NoAuth_Returns401()
    {
        var (client, _, _) = CreateSetup();
        var resp = await client.GetAsync("/api/users/me/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Generate_ScopedToCallingUser_OtherUserSeesNotFound()
    {
        var (client, _, _) = CreateSetup();
        var traits = FakeTraitsExtractor.BuildDefaultTraits();
        await SeedGarmentAsync(client, traits, "user-a");

        var genResp = await client.SendAsync(AuthedPost("/api/users/me/profile/generate", null, "user-a"));
        genResp.EnsureSuccessStatusCode();

        var getResp = await client.SendAsync(AuthedGet("/api/users/me/profile", "user-b"));
        Assert.Equal(HttpStatusCode.NotFound, getResp.StatusCode);
    }

    [Fact]
    public async Task Regenerate_PreservesCreatedAtAndUpdatesModifiedAt()
    {
        var (client, _, _) = CreateSetup();
        var traits = FakeTraitsExtractor.BuildDefaultTraits();
        await SeedGarmentAsync(client, traits);

        var first = await (await client.SendAsync(AuthedPost("/api/users/me/profile/generate", null)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var firstCreatedAt = first.GetProperty("createdAt").GetString();

        // Small delay so timestamps differ
        await Task.Delay(10);

        var second = await (await client.SendAsync(AuthedPost("/api/users/me/profile/generate", null)))
            .Content.ReadFromJsonAsync<JsonElement>(JsonOpts);

        Assert.Equal(firstCreatedAt, second.GetProperty("createdAt").GetString());
        Assert.NotEqual(
            first.GetProperty("modifiedAt").GetString(),
            second.GetProperty("modifiedAt").GetString());
    }
}
