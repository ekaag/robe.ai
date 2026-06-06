using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Robe.Core.Interfaces;
using Robe.Infrastructure.Auth;
using Robe.Infrastructure.Persistence;
using Robe.Infrastructure.Profile;
using Robe.Infrastructure.Recommendations;
using Robe.Infrastructure.Secrets;
using Robe.Infrastructure.Storage;
using Robe.Infrastructure.TraitsExtraction;

namespace Robe.Api.Tests;

public class MeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public MeTests(WebApplicationFactory<Program> factory) => _factory = factory;

    private HttpClient CreateClient() =>
        _factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<ISecretManager>();
                services.AddSingleton<ISecretManager>(new MockSecretManager(new Dictionary<string, string>()));

                services.RemoveAll<ITraitsExtractor>();
                services.AddScoped<ITraitsExtractor>(_ => FakeTraitsExtractor.ReturnsDefault());

                services.RemoveAll<IGarmentRepository>();
                services.AddSingleton<IGarmentRepository>(new InMemoryGarmentRepository());

                services.RemoveAll<IImageStore>();
                services.AddSingleton<IImageStore>(new InMemoryImageStore());

                services.RemoveAll<IProfileGenerator>();
                services.AddScoped<IProfileGenerator, FakeProfileGenerator>();

                services.RemoveAll<IProfileRepository>();
                services.AddSingleton<IProfileRepository>(new InMemoryProfileRepository());

                services.RemoveAll<IRecommender>();
                services.AddScoped<IRecommender, FakeRecommender>();

                services.RemoveAll<IInventoryRepository>();
                services.AddSingleton<IInventoryRepository>(new InMemoryInventoryRepository());

                services.RemoveAll<ICurrentUser>();
                services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
            });
        }).CreateClient();

    private static HttpRequestMessage AuthedGet(
        string userId,
        string? name     = null,
        string? provider = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        req.Headers.Add(LocalAuthHandler.UserIdHeader, userId);
        if (name     is not null) req.Headers.Add(LocalAuthHandler.UserNameHeader, name);
        if (provider is not null) req.Headers.Add(LocalAuthHandler.ProviderHeader,  provider);
        return req;
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Get_ValidToken_Returns200WithUserId()
    {
        var client = CreateClient();
        var resp   = await client.SendAsync(AuthedGet("usr-123"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal("usr-123", body.GetProperty("userId").GetString());
    }

    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var client = CreateClient();
        var resp   = await client.GetAsync("/api/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Get_EmptyUserIdHeader_Returns401()
    {
        var client = CreateClient();
        var req    = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        req.Headers.Add(LocalAuthHandler.UserIdHeader, "   ");

        var resp = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Get_UserId_MatchesTokenSubject_NotClientSupplied()
    {
        var client = CreateClient();

        // Authenticate as "user-a"; the body must reflect that, not any other value.
        var resp = await client.SendAsync(AuthedGet("user-a"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal("user-a", body.GetProperty("userId").GetString());

        // Re-run as a different user; response must change accordingly.
        var resp2 = await client.SendAsync(AuthedGet("user-b"));
        var body2 = await resp2.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal("user-b", body2.GetProperty("userId").GetString());
    }

    [Fact]
    public async Task Get_WithNameAndProvider_ReturnsBothInResponse()
    {
        var client = CreateClient();
        var resp   = await client.SendAsync(AuthedGet("usr-123", name: "Sundeep A.", provider: "apple"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal("Sundeep A.", body.GetProperty("name").GetString());
        Assert.Equal("apple",      body.GetProperty("provider").GetString());
    }

    [Fact]
    public async Task Get_WithoutNameOrProvider_ReturnsNullForBoth()
    {
        var client = CreateClient();
        var resp   = await client.SendAsync(AuthedGet("usr-123"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("name").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("provider").ValueKind);
    }
}
