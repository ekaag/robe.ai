using Azure;
using Robe.Infrastructure.Secrets.Azure;

namespace Robe.Api.Tests;

internal sealed class FakeSecretFetcher : ISecretFetcher
{
    private readonly Dictionary<string, string> _values;

    public FakeSecretFetcher(Dictionary<string, string> values) => _values = values;

    public int CallCount { get; private set; }
    public List<string> RequestedNames { get; } = new();

    public Task<string> GetSecretValueAsync(string keyVaultSecretName, CancellationToken ct)
    {
        CallCount++;
        RequestedNames.Add(keyVaultSecretName);
        return _values.TryGetValue(keyVaultSecretName, out var value)
            ? Task.FromResult(value)
            : throw new RequestFailedException(404, $"Secret '{keyVaultSecretName}' not found.");
    }
}

public class AzureKeyVaultSecretManagerTests
{
    [Fact]
    public void ToKeyVaultSecretName_MapsColonToDoubleDash()
    {
        Assert.Equal("AzureOpenAI--ApiKey", AzureKeyVaultSecretManager.ToKeyVaultSecretName("AzureOpenAI:ApiKey"));
    }

    [Fact]
    public async Task GetSecretAsync_RequestsNameMappedToDoubleDash()
    {
        var fetcher = new FakeSecretFetcher(new Dictionary<string, string> { ["AzureOpenAI--ApiKey"] = "secret-value" });
        var manager = new AzureKeyVaultSecretManager(fetcher);

        var value = await manager.GetSecretAsync("AzureOpenAI:ApiKey");

        Assert.Equal("secret-value", value);
        Assert.Equal("AzureOpenAI--ApiKey", Assert.Single(fetcher.RequestedNames));
    }

    [Fact]
    public async Task GetSecretAsync_WhenNotFound_ThrowsKeyNotFoundException()
    {
        var fetcher = new FakeSecretFetcher(new Dictionary<string, string>());
        var manager = new AzureKeyVaultSecretManager(fetcher);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => manager.GetSecretAsync("Missing:Secret"));
    }

    [Fact]
    public async Task GetSecretAsync_WithinTtl_ReturnsCachedValueWithoutRefetching()
    {
        var fetcher = new FakeSecretFetcher(new Dictionary<string, string> { ["AzureOpenAI--ApiKey"] = "secret-value" });
        var manager = new AzureKeyVaultSecretManager(fetcher, cacheTtl: TimeSpan.FromMinutes(10));

        await manager.GetSecretAsync("AzureOpenAI:ApiKey");
        await manager.GetSecretAsync("AzureOpenAI:ApiKey");

        Assert.Equal(1, fetcher.CallCount);
    }

    [Fact]
    public async Task GetSecretAsync_AfterTtlExpires_RefetchesFromKeyVault()
    {
        var fetcher = new FakeSecretFetcher(new Dictionary<string, string> { ["AzureOpenAI--ApiKey"] = "secret-value" });
        var manager = new AzureKeyVaultSecretManager(fetcher, cacheTtl: TimeSpan.Zero);

        await manager.GetSecretAsync("AzureOpenAI:ApiKey");
        await manager.GetSecretAsync("AzureOpenAI:ApiKey");

        Assert.Equal(2, fetcher.CallCount);
    }
}
