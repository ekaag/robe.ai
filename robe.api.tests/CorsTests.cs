using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Robe.Core.Interfaces;
using Robe.Infrastructure.Persistence;
using Robe.Infrastructure.Profile;
using Robe.Infrastructure.Recommendations;
using Robe.Infrastructure.Secrets;
using Robe.Infrastructure.Storage;
using Robe.Infrastructure.TraitsExtraction;

namespace Robe.Api.Tests;

public class CorsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CorsTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // Replace ICorsPolicyProvider entirely so the test owns exactly which origins are allowed.
    // CorsMiddleware calls GetPolicyAsync per request — this bypasses CorsOptions wiring.
    private HttpClient CreateClient(params string[] allowedOrigins)
    {
        CorsPolicy? policy = null;
        if (allowedOrigins.Length > 0)
        {
            var builder = new CorsPolicyBuilder();
            builder.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
            policy = builder.Build();
        }

        return _factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.Replace(ServiceDescriptor.Singleton<ICorsPolicyProvider>(
                    new FixedCorsPolicyProvider("AllowConfiguredOrigins", policy)));

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
            });
        }).CreateClient();
    }

    [Fact]
    public async Task Options_AllowedOrigin_ReturnsAccessControlAllowOriginHeader()
    {
        var client = CreateClient("http://localhost:3000");

        var req = new HttpRequestMessage(HttpMethod.Options, "/api/me");
        req.Headers.Add("Origin", "http://localhost:3000");
        req.Headers.Add("Access-Control-Request-Method", "GET");

        var resp = await client.SendAsync(req);

        Assert.True(
            resp.Headers.Contains("Access-Control-Allow-Origin"),
            $"Expected CORS header on preflight; status was {resp.StatusCode}");
        Assert.Equal(
            "http://localhost:3000",
            resp.Headers.GetValues("Access-Control-Allow-Origin").First());
    }

    [Fact]
    public async Task Options_DisallowedOrigin_DoesNotReturnCorsHeader()
    {
        var client = CreateClient("http://localhost:3000");

        var req = new HttpRequestMessage(HttpMethod.Options, "/api/me");
        req.Headers.Add("Origin", "http://evil.example.com");
        req.Headers.Add("Access-Control-Request-Method", "GET");

        var resp = await client.SendAsync(req);

        Assert.False(
            resp.Headers.Contains("Access-Control-Allow-Origin"),
            "Should not echo CORS header for unlisted origin");
    }

    [Fact]
    public async Task Options_NoOriginsConfigured_DoesNotReturnCorsHeader()
    {
        // Empty allowed-origins list — simulates production with no CORS configured
        var client = CreateClient();

        var req = new HttpRequestMessage(HttpMethod.Options, "/api/me");
        req.Headers.Add("Origin", "http://localhost:3000");
        req.Headers.Add("Access-Control-Request-Method", "GET");

        var resp = await client.SendAsync(req);

        Assert.False(
            resp.Headers.Contains("Access-Control-Allow-Origin"),
            "Should not return CORS headers when no origins are configured");
    }

    // Returns a fixed policy (or null) for the named policy; all other names → null.
    private sealed class FixedCorsPolicyProvider : ICorsPolicyProvider
    {
        private readonly string _name;
        private readonly CorsPolicy? _policy;

        public FixedCorsPolicyProvider(string name, CorsPolicy? policy)
        {
            _name = name;
            _policy = policy;
        }

        public Task<CorsPolicy?> GetPolicyAsync(HttpContext context, string? policyName) =>
            Task.FromResult(policyName == _name ? _policy : null);
    }
}
