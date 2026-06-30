using System.Collections.Concurrent;
using Azure;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Secrets.Azure;

public class AzureKeyVaultSecretManager : ISecretManager
{
    private static readonly TimeSpan DefaultCacheTtl = TimeSpan.FromMinutes(10);

    private readonly ISecretFetcher _fetcher;
    private readonly TimeSpan _cacheTtl;
    private readonly ConcurrentDictionary<string, (string Value, DateTimeOffset ExpiresAt)> _cache = new();

    // Production entrypoint — auth via DefaultAzureCredential (managed identity in Azure,
    // falls back to az/VS credentials for local testing against a real vault).
    public AzureKeyVaultSecretManager(string vaultUri, TimeSpan? cacheTtl = null)
        : this(new AzureSecretClientFetcher(new SecretClient(new Uri(vaultUri), new DefaultAzureCredential())), cacheTtl)
    {
    }

    internal AzureKeyVaultSecretManager(ISecretFetcher fetcher, TimeSpan? cacheTtl = null)
    {
        _fetcher = fetcher;
        _cacheTtl = cacheTtl ?? DefaultCacheTtl;
    }

    public async Task<string> GetSecretAsync(string name, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(name, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
            return cached.Value;

        try
        {
            var value = await _fetcher.GetSecretValueAsync(ToKeyVaultSecretName(name), ct);
            _cache[name] = (value, DateTimeOffset.UtcNow + _cacheTtl);
            return value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new KeyNotFoundException($"Secret '{name}' not found in Azure Key Vault.", ex);
        }
    }

    // Key Vault secret names can't contain ':' — mirrors the convention .NET's own
    // AddAzureKeyVault configuration provider uses ("AzureOpenAI:ApiKey" -> "AzureOpenAI--ApiKey").
    internal static string ToKeyVaultSecretName(string name) => name.Replace(":", "--");
}
