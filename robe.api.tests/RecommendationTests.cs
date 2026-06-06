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
using Robe.Infrastructure.Recommendations;
using Robe.Infrastructure.Secrets;
using Robe.Infrastructure.Storage;
using Robe.Infrastructure.TraitsExtraction;

namespace Robe.Api.Tests;

public class RecommendationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public RecommendationTests(WebApplicationFactory<Program> factory) => _factory = factory;

    // ── Setup helpers ────────────────────────────────────────────────────────

    private (HttpClient client, InMemoryInventoryRepository inventoryRepo, InMemoryProfileRepository profileRepo)
        CreateSetup(IEnumerable<InventoryItem>? inventory = null)
    {
        var inventoryRepo = new InMemoryInventoryRepository(inventory);
        var profileRepo   = new InMemoryProfileRepository();

        var client = _factory.WithWebHostBuilder(b =>
        {
            b.ConfigureTestServices(services =>
            {
                services.RemoveAll<ITraitsExtractor>();
                services.AddScoped<ITraitsExtractor>(_ => FakeTraitsExtractor.ReturnsDefault());

                services.RemoveAll<ISecretManager>();
                services.AddSingleton<ISecretManager>(new MockSecretManager(new Dictionary<string, string>()));

                services.RemoveAll<IGarmentRepository>();
                services.AddSingleton<IGarmentRepository>(new InMemoryGarmentRepository());

                services.RemoveAll<IImageStore>();
                services.AddSingleton<IImageStore>(new InMemoryImageStore());

                services.RemoveAll<IProfileGenerator>();
                services.AddScoped<IProfileGenerator, FakeProfileGenerator>();

                services.RemoveAll<IProfileRepository>();
                services.AddSingleton<IProfileRepository>(profileRepo);

                services.RemoveAll<IRecommender>();
                services.AddScoped<IRecommender, FakeRecommender>();

                services.RemoveAll<IInventoryRepository>();
                services.AddSingleton<IInventoryRepository>(inventoryRepo);

                services.RemoveAll<ICurrentUser>();
                services.AddScoped<ICurrentUser, HttpContextCurrentUser>();
            });
        }).CreateClient();

        return (client, inventoryRepo, profileRepo);
    }

    private static StyleProfile MakeProfile(string userId = "user-a")
    {
        var now = DateTimeOffset.UtcNow;
        return new StyleProfile
        {
            DominantStyles   = new List<string> { "minimalist", "casual" },
            ColorPalette     = new List<ColorWeight> { new("navy", "#1f2a44", 1.0) },
            FormalityRange   = new FormalityRange(1, 3, 2.0),
            PreferredFits    = new List<string> { "regular" },
            SeasonalSkew     = new Dictionary<string, double> { ["spring"] = 0.5, ["summer"] = 0.5 },
            Summary          = "Minimalist casual wardrobe.",
            GarmentCount     = 2,
            CreatedAt        = now,
            ModifiedAt       = now,
            CreatedByUserId  = userId,
            ModifiedByUserId = userId
        };
    }

    private static InventoryItem MakeItem(
        string id,
        GarmentTraits traits,
        decimal price,
        bool inStock = true)
    {
        var now = DateTimeOffset.UtcNow;
        return new InventoryItem
        {
            Id               = id,
            VendorId         = "vendor-1",
            Name             = $"Item {id}",
            Description      = "",
            Traits           = traits,
            Price            = price,
            Currency         = "USD",
            Url              = $"https://example.com/{id}",
            ImageUrl         = $"https://example.com/{id}.jpg",
            InStock          = inStock,
            CreatedAt        = now,
            ModifiedAt       = now,
            CreatedByUserId  = "system",
            ModifiedByUserId = "system"
        };
    }

    private static GarmentTraits MinimalistTop(string? occasion = null) => new GarmentTraits
    {
        Category        = GarmentCategory.Top,
        Subcategory     = "t-shirt",
        PrimaryColor    = new ColorInfo("navy", "#1f2a44"),
        SecondaryColors = new List<ColorInfo>(),
        Pattern         = GarmentPattern.Solid,
        Formality       = 2,
        Seasonality     = new List<Season> { Season.Spring, Season.Summer },
        StyleTags       = new List<string> { "minimalist", "casual" },
        Occasions       = occasion is null
            ? new List<string> { "everyday" }
            : new List<string> { occasion },
        Confidence      = 0.9
    };

    private static GarmentTraits CasualBottom() => new GarmentTraits
    {
        Category        = GarmentCategory.Bottom,
        Subcategory     = "jeans",
        PrimaryColor    = new ColorInfo("blue", "#0000ff"),
        SecondaryColors = new List<ColorInfo>(),
        Pattern         = GarmentPattern.Solid,
        Formality       = 1,
        Seasonality     = new List<Season> { Season.All },
        StyleTags       = new List<string> { "casual" },
        Occasions       = new List<string> { "everyday" },
        Confidence      = 0.85
    };

    private static HttpContent RecommendBody(
        decimal? maxBudget = null,
        List<GarmentCategory>? categories = null,
        string? occasion = null,
        int count = 5) =>
        new StringContent(
            JsonSerializer.Serialize(
                new { maxBudget, categories, occasion, count },
                JsonOpts),
            Encoding.UTF8, "application/json");

    private static HttpRequestMessage AuthedPost(string url, HttpContent? body, string userId = "user-a")
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = body };
        req.Headers.Add(LocalAuthHandler.UserIdHeader, userId);
        return req;
    }

    // ── Tests ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Recommend_WithProfile_ReturnsRankedItems()
    {
        var items = new[]
        {
            MakeItem("i1", MinimalistTop(), 50m),
            MakeItem("i2", CasualBottom(),  40m)
        };
        var (client, _, profileRepo) = CreateSetup(items);
        await profileRepo.SaveAsync("user-a", MakeProfile(), CancellationToken.None);

        var resp = await client.SendAsync(AuthedPost(
            "/api/users/me/recommendations", RecommendBody(count: 5)));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var recs = body.GetProperty("recommendations").EnumerateArray().ToList();
        Assert.NotEmpty(recs);

        // FakeRecommender scores minimalist top higher (2 tag matches) than casual bottom (1)
        var first = recs[0].GetProperty("inventoryItem").GetProperty("id").GetString();
        Assert.Equal("i1", first);

        Assert.True(body.GetProperty("recommendations")[0].GetProperty("score").GetDouble() > 0);
        Assert.False(string.IsNullOrEmpty(body.GetProperty("requestedAt").GetString()));
    }

    [Fact]
    public async Task Recommend_BudgetFilter_ExcludesOverBudgetItems()
    {
        var items = new[]
        {
            MakeItem("cheap",      MinimalistTop(), 49.99m),
            MakeItem("expensive",  MinimalistTop(), 200m)
        };
        var (client, _, profileRepo) = CreateSetup(items);
        await profileRepo.SaveAsync("user-a", MakeProfile(), CancellationToken.None);

        var resp = await client.SendAsync(AuthedPost(
            "/api/users/me/recommendations", RecommendBody(maxBudget: 50m, count: 10)));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var recs = body.GetProperty("recommendations").EnumerateArray().ToList();

        Assert.All(recs, r =>
            Assert.NotEqual("expensive", r.GetProperty("inventoryItem").GetProperty("id").GetString()));
        Assert.Contains(recs, r =>
            r.GetProperty("inventoryItem").GetProperty("id").GetString() == "cheap");
    }

    [Fact]
    public async Task Recommend_CategoryFilter_ExcludesWrongCategories()
    {
        var items = new[]
        {
            MakeItem("top1",    MinimalistTop(), 50m),
            MakeItem("bottom1", CasualBottom(),  40m)
        };
        var (client, _, profileRepo) = CreateSetup(items);
        await profileRepo.SaveAsync("user-a", MakeProfile(), CancellationToken.None);

        var resp = await client.SendAsync(AuthedPost(
            "/api/users/me/recommendations",
            RecommendBody(categories: new List<GarmentCategory> { GarmentCategory.Top }, count: 10)));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var recs = body.GetProperty("recommendations").EnumerateArray().ToList();

        Assert.All(recs, r =>
            Assert.Equal("top", r.GetProperty("inventoryItem").GetProperty("traits").GetProperty("category").GetString()));
        Assert.DoesNotContain(recs, r =>
            r.GetProperty("inventoryItem").GetProperty("id").GetString() == "bottom1");
    }

    [Fact]
    public async Task Recommend_OccasionFilter_ExcludesWrongOccasions()
    {
        var items = new[]
        {
            MakeItem("work-top",    MinimalistTop("work"),    60m),
            MakeItem("casual-top",  MinimalistTop("everyday"), 50m)
        };
        var (client, _, profileRepo) = CreateSetup(items);
        await profileRepo.SaveAsync("user-a", MakeProfile(), CancellationToken.None);

        var resp = await client.SendAsync(AuthedPost(
            "/api/users/me/recommendations", RecommendBody(occasion: "work", count: 10)));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        var recs = body.GetProperty("recommendations").EnumerateArray().ToList();

        Assert.Single(recs);
        Assert.Equal("work-top", recs[0].GetProperty("inventoryItem").GetProperty("id").GetString());
    }

    [Fact]
    public async Task Recommend_CountCaps_AtRequestedCount()
    {
        var items = Enumerable.Range(1, 10)
            .Select(i => MakeItem($"item-{i}", MinimalistTop(), 50m))
            .ToArray();
        var (client, _, profileRepo) = CreateSetup(items);
        await profileRepo.SaveAsync("user-a", MakeProfile(), CancellationToken.None);

        var resp = await client.SendAsync(AuthedPost(
            "/api/users/me/recommendations", RecommendBody(count: 3)));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(3, body.GetProperty("recommendations").GetArrayLength());
    }

    [Fact]
    public async Task Recommend_NoProfile_Returns404()
    {
        var (client, _, _) = CreateSetup();

        var resp = await client.SendAsync(AuthedPost(
            "/api/users/me/recommendations", RecommendBody()));
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Recommend_EmptyInventory_Returns200WithEmptyList()
    {
        var (client, _, profileRepo) = CreateSetup(); // no inventory items
        await profileRepo.SaveAsync("user-a", MakeProfile(), CancellationToken.None);

        var resp = await client.SendAsync(AuthedPost(
            "/api/users/me/recommendations", RecommendBody()));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var body = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        Assert.Equal(0, body.GetProperty("recommendations").GetArrayLength());
    }

    [Fact]
    public async Task Recommend_NoAuth_Returns401()
    {
        var (client, _, _) = CreateSetup();
        var resp = await client.PostAsync("/api/users/me/recommendations", RecommendBody());
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
