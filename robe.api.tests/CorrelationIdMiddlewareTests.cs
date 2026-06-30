using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Robe.Api.Middleware;
using Robe.Core.Interfaces;
using Robe.Infrastructure.Auth;
using Robe.Infrastructure.Persistence;
using Robe.Infrastructure.Profile;
using Robe.Infrastructure.Recommendations;
using Robe.Infrastructure.Secrets;
using Robe.Infrastructure.Storage;
using Robe.Infrastructure.TraitsExtraction;

namespace Robe.Api.Tests;

public class CorrelationIdMiddlewareTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CorrelationIdMiddlewareTests(WebApplicationFactory<Program> factory) => _factory = factory;

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

    private static HttpRequestMessage AuthedGet(string url, string userId = "user-a")
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add(LocalAuthHandler.UserIdHeader, userId);
        return req;
    }

    [Fact]
    public async Task Response_AlwaysIncludesCorrelationIdHeader()
    {
        var client = CreateClient();

        var resp = await client.SendAsync(AuthedGet("/api/me"));

        Assert.True(resp.Headers.Contains(CorrelationIdMiddleware.HeaderName));
        Assert.False(string.IsNullOrWhiteSpace(resp.Headers.GetValues(CorrelationIdMiddleware.HeaderName).First()));
    }

    [Fact]
    public async Task Request_WithExistingCorrelationId_IsEchoedBackUnchanged()
    {
        var client = CreateClient();
        var req = AuthedGet("/api/me");
        req.Headers.Add(CorrelationIdMiddleware.HeaderName, "client-supplied-id-123");

        var resp = await client.SendAsync(req);

        Assert.Equal("client-supplied-id-123", resp.Headers.GetValues(CorrelationIdMiddleware.HeaderName).First());
    }

    [Fact]
    public async Task Request_WithoutCorrelationId_GeneratesANewOne()
    {
        var client = CreateClient();

        var resp = await client.SendAsync(AuthedGet("/api/me"));

        var generated = resp.Headers.GetValues(CorrelationIdMiddleware.HeaderName).First();
        Assert.True(Guid.TryParse(generated, out _));
    }

    [Fact]
    public async Task CorrelationId_IsPresentRegardlessOfResponseStatusCode()
    {
        var client = CreateClient();

        var resp = await client.SendAsync(AuthedGet("/api/garments/grm_doesnotexist"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.True(resp.Headers.Contains(CorrelationIdMiddleware.HeaderName));
    }
}
