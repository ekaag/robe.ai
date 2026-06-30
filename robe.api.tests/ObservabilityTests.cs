using Microsoft.Extensions.Logging.Abstractions;
using Robe.Core.Domain;
using Robe.Core.Interfaces;
using Robe.Core.Observability;
using Robe.Infrastructure.Observability;
using Robe.Infrastructure.Observability.Decorators;
using Robe.Infrastructure.Observability.Local;
using Robe.Infrastructure.Persistence;
using Robe.Infrastructure.Profile;
using Robe.Infrastructure.Recommendations;
using Robe.Infrastructure.TraitsExtraction;

namespace Robe.Api.Tests;

public class LocalLogServiceTests
{
    private static LocalLogService CreateService(ICorrelationContextAccessor? correlation = null) =>
        new(NullLogger<LocalLogService>.Instance, correlation ?? new AsyncLocalCorrelationContextAccessor());

    [Fact]
    public void Log_CapturesSeverityMessageAndProperties()
    {
        var service = CreateService();

        service.Log(LogSeverity.Warning, "something happened", new Dictionary<string, object?> { ["garmentId"] = "grm_1" });

        var entry = Assert.Single(service.RecentEntries);
        Assert.Equal(LogSeverity.Warning, entry.Severity);
        Assert.Equal("something happened", entry.Message);
        Assert.Equal("grm_1", entry.Properties?["garmentId"]);
    }

    [Fact]
    public void Log_AttachesCurrentCorrelationId()
    {
        var correlation = new AsyncLocalCorrelationContextAccessor { CorrelationId = "corr-123" };
        var service = CreateService(correlation);

        service.Log(LogSeverity.Information, "hello");

        Assert.Equal("corr-123", Assert.Single(service.RecentEntries).CorrelationId);
    }

    [Fact]
    public void Log_AttachesCurrentUserId()
    {
        var correlation = new AsyncLocalCorrelationContextAccessor { UserId = "user-a" };
        var service = CreateService(correlation);

        service.Log(LogSeverity.Information, "hello");

        Assert.Equal("user-a", Assert.Single(service.RecentEntries).UserId);
    }

    [Fact]
    public void Log_WithNoAuthenticatedUser_LeavesUserIdEmpty()
    {
        var service = CreateService();

        service.Log(LogSeverity.Information, "hello");

        Assert.Equal("", Assert.Single(service.RecentEntries).UserId);
    }

    [Fact]
    public void Log_CapturesExceptionWhenProvided()
    {
        var service = CreateService();
        var ex = new InvalidOperationException("boom");

        service.Log(LogSeverity.Error, "failed", exception: ex);

        Assert.Same(ex, Assert.Single(service.RecentEntries).Exception);
    }

    [Fact]
    public void Info_ExtensionMethod_LogsAtInformationSeverity()
    {
        var service = CreateService();

        service.Info("informational message");

        Assert.Equal(LogSeverity.Information, Assert.Single(service.RecentEntries).Severity);
    }
}

public class LocalMetricsServiceTests
{
    private static LocalMetricsService CreateService(ICorrelationContextAccessor? correlation = null) =>
        new(NullLogger<LocalMetricsService>.Instance, correlation ?? new AsyncLocalCorrelationContextAccessor());

    [Fact]
    public void Increment_RecordsCounterEntry()
    {
        var service = CreateService();

        service.Increment("garments.stored");

        var entry = Assert.Single(service.RecentEntries);
        Assert.Equal(MetricKind.Counter, entry.Kind);
        Assert.Equal("garments.stored", entry.Name);
        Assert.Equal(1, entry.Value);
    }

    [Fact]
    public void RecordValue_RecordsValueEntryWithTags()
    {
        var service = CreateService();

        service.RecordValue("http.request_duration_ms", 42.5, new Dictionary<string, string> { ["statusCode"] = "200" });

        var entry = Assert.Single(service.RecentEntries);
        Assert.Equal(MetricKind.Value, entry.Kind);
        Assert.Equal(42.5, entry.Value);
        Assert.Equal("200", entry.Tags?["statusCode"]);
    }

    [Fact]
    public void Increment_AttachesCurrentUserId()
    {
        var correlation = new AsyncLocalCorrelationContextAccessor { UserId = "user-a" };
        var service = CreateService(correlation);

        service.Increment("garments.stored");

        Assert.Equal("user-a", Assert.Single(service.RecentEntries).UserId);
    }

    [Fact]
    public void Time_RecordsElapsedMillisecondsOnDispose()
    {
        var service = CreateService();

        using (service.Time("garment_traits.extract_duration_ms")) { }

        var entry = Assert.Single(service.RecentEntries);
        Assert.Equal(MetricKind.Value, entry.Kind);
        Assert.True(entry.Value >= 0);
    }
}

public class LocalAlertServiceTests
{
    private static LocalAlertService CreateService(ICorrelationContextAccessor? correlation = null) =>
        new(NullLogger<LocalAlertService>.Instance, correlation ?? new AsyncLocalCorrelationContextAccessor());

    [Fact]
    public async Task RaiseAsync_CapturesSeverityMessageAndContext()
    {
        var service = CreateService();

        await service.RaiseAsync(AlertSeverity.Critical, "Azure OpenAI call failed",
            new Dictionary<string, object?> { ["exception"] = "ExtractionException" });

        var entry = Assert.Single(service.RecentEntries);
        Assert.Equal(AlertSeverity.Critical, entry.Severity);
        Assert.Equal("Azure OpenAI call failed", entry.Message);
        Assert.Equal("ExtractionException", entry.Context?["exception"]);
    }

    [Fact]
    public async Task RaiseAsync_AttachesCurrentUserId()
    {
        var correlation = new AsyncLocalCorrelationContextAccessor { UserId = "user-a" };
        var service = CreateService(correlation);

        await service.RaiseAsync(AlertSeverity.Critical, "Azure OpenAI call failed");

        Assert.Equal("user-a", Assert.Single(service.RecentEntries).UserId);
    }
}

public class ObservableTraitsExtractorTests
{
    private readonly LocalLogService _log = new(NullLogger<LocalLogService>.Instance, new AsyncLocalCorrelationContextAccessor());
    private readonly LocalMetricsService _metrics = new(NullLogger<LocalMetricsService>.Instance, new AsyncLocalCorrelationContextAccessor());
    private readonly LocalAlertService _alerts = new(NullLogger<LocalAlertService>.Instance, new AsyncLocalCorrelationContextAccessor());

    private static readonly ImageInput Image = new(new byte[16], "image/jpeg");

    [Fact]
    public async Task ExtractAsync_OnSuccess_IncrementsCounterAndLogsInfo()
    {
        var decorator = new ObservableTraitsExtractor(FakeTraitsExtractor.ReturnsDefault(), _log, _metrics, _alerts);

        var traits = await decorator.ExtractAsync(Image);

        Assert.NotNull(traits);
        Assert.Contains(_metrics.RecentEntries, m => m.Name == "garment_traits.analyzed" && m.Kind == MetricKind.Counter);
        Assert.Contains(_log.RecentEntries, e => e.Severity == LogSeverity.Information);
        Assert.Empty(_alerts.RecentEntries);
    }

    [Fact]
    public async Task ExtractAsync_OnFailure_IncrementsFailureCounterLogsErrorAndRaisesAlert_ThenRethrows()
    {
        var thrown = new InvalidOperationException("Azure OpenAI call failed.");
        var decorator = new ObservableTraitsExtractor(FakeTraitsExtractor.Throws(thrown), _log, _metrics, _alerts);

        var actual = await Assert.ThrowsAsync<InvalidOperationException>(() => decorator.ExtractAsync(Image));

        Assert.Same(thrown, actual);
        Assert.Contains(_metrics.RecentEntries, m => m.Name == "garment_traits.failed");
        Assert.Contains(_log.RecentEntries, e => e.Severity == LogSeverity.Error && e.Exception == thrown);
        Assert.Single(_alerts.RecentEntries);
    }
}

public class ObservableProfileGeneratorTests
{
    private readonly LocalLogService _log = new(NullLogger<LocalLogService>.Instance, new AsyncLocalCorrelationContextAccessor());
    private readonly LocalMetricsService _metrics = new(NullLogger<LocalMetricsService>.Instance, new AsyncLocalCorrelationContextAccessor());
    private readonly LocalAlertService _alerts = new(NullLogger<LocalAlertService>.Instance, new AsyncLocalCorrelationContextAccessor());

    [Fact]
    public async Task GenerateAsync_OnSuccess_IncrementsCounterAndLogsInfo()
    {
        var decorator = new ObservableProfileGenerator(new FakeProfileGenerator(), _log, _metrics, _alerts);

        var profile = await decorator.GenerateAsync(new[] { FakeTraitsExtractor.BuildDefaultTraits() });

        Assert.NotNull(profile);
        Assert.Contains(_metrics.RecentEntries, m => m.Name == "profile.generated");
        Assert.Empty(_alerts.RecentEntries);
    }

    [Fact]
    public async Task GenerateAsync_OnFailure_RaisesAlert_ThenRethrows()
    {
        // AzureOpenAIProfileGenerator is currently an unimplemented stub — exercises the real failure path.
        var decorator = new ObservableProfileGenerator(new AzureOpenAIProfileGenerator(), _log, _metrics, _alerts);

        await Assert.ThrowsAsync<NotImplementedException>(() => decorator.GenerateAsync(Array.Empty<GarmentTraits>()));

        Assert.Contains(_metrics.RecentEntries, m => m.Name == "profile.generation_failed");
        Assert.Single(_alerts.RecentEntries);
    }
}

public class ObservableRecommenderTests
{
    private readonly LocalLogService _log = new(NullLogger<LocalLogService>.Instance, new AsyncLocalCorrelationContextAccessor());
    private readonly LocalMetricsService _metrics = new(NullLogger<LocalMetricsService>.Instance, new AsyncLocalCorrelationContextAccessor());
    private readonly LocalAlertService _alerts = new(NullLogger<LocalAlertService>.Instance, new AsyncLocalCorrelationContextAccessor());

    [Fact]
    public async Task RecommendAsync_OnSuccess_RecordsServedCountAndAvgScore()
    {
        var decorator = new ObservableRecommender(new FakeRecommender(), _log, _metrics, _alerts);
        var now = DateTimeOffset.UtcNow;
        var traits = FakeTraitsExtractor.BuildDefaultTraits();

        var profile = new StyleProfile
        {
            DominantStyles = new List<string> { "minimalist" },
            CreatedAt = now,
            ModifiedAt = now,
            CreatedByUserId = "user-a",
            ModifiedByUserId = "user-a"
        };
        var candidates = new List<InventoryItem>
        {
            new()
            {
                Id = "inv_1", VendorId = "nordstrom", Name = "Tee", Traits = traits,
                Price = 20, Url = "https://example.com", ImageUrl = "https://example.com/i.jpg",
                InStock = true, CreatedAt = now, ModifiedAt = now, CreatedByUserId = "user-a", ModifiedByUserId = "user-a"
            }
        };

        var recommendations = await decorator.RecommendAsync(profile, candidates, new RecommendationContext());

        Assert.NotEmpty(recommendations);
        Assert.Contains(_metrics.RecentEntries, m => m.Name == "recommendations.served" && m.Value == recommendations.Count);
        Assert.Contains(_metrics.RecentEntries, m => m.Name == "recommendations.avg_score");
        Assert.Empty(_alerts.RecentEntries);
    }
}

public class ObservableGarmentRepositoryTests
{
    private readonly LocalLogService _log = new(NullLogger<LocalLogService>.Instance, new AsyncLocalCorrelationContextAccessor());
    private readonly LocalMetricsService _metrics = new(NullLogger<LocalMetricsService>.Instance, new AsyncLocalCorrelationContextAccessor());

    [Fact]
    public async Task AddAsync_IncrementsStoredCounter()
    {
        var decorator = new ObservableGarmentRepository(new InMemoryGarmentRepository(), _log, _metrics);
        var now = DateTimeOffset.UtcNow;

        await decorator.AddAsync(new Garment
        {
            Id = "grm_1",
            UserId = "user-a",
            Traits = FakeTraitsExtractor.BuildDefaultTraits(),
            ImageUrl = "https://example.com/grm_1.jpg",
            BlobKey = "grm_1.jpg",
            CreatedAt = now,
            ModifiedAt = now,
            CreatedByUserId = "user-a",
            ModifiedByUserId = "user-a"
        });

        Assert.Contains(_metrics.RecentEntries, m => m.Name == "garments.stored");
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_DoesNotIncrementCounter()
    {
        var decorator = new ObservableGarmentRepository(new InMemoryGarmentRepository(), _log, _metrics);

        var deleted = await decorator.DeleteAsync("does-not-exist", "user-a");

        Assert.False(deleted);
        Assert.DoesNotContain(_metrics.RecentEntries, m => m.Name == "garments.deleted");
    }
}
