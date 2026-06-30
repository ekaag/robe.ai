using Azure.Security.KeyVault.Secrets;

namespace Robe.Infrastructure.Secrets.Azure;

// Thin seam around SecretClient so AzureKeyVaultSecretManager's caching/name-mapping/error-wrapping
// logic can be unit tested with a hand-written fake instead of a mocking library — matches this
// project's existing Fake*/MockSecretManager testing convention.
internal interface ISecretFetcher
{
    Task<string> GetSecretValueAsync(string keyVaultSecretName, CancellationToken ct);
}

internal sealed class AzureSecretClientFetcher : ISecretFetcher
{
    private readonly SecretClient _client;

    public AzureSecretClientFetcher(SecretClient client) => _client = client;

    public async Task<string> GetSecretValueAsync(string keyVaultSecretName, CancellationToken ct)
    {
        var response = await _client.GetSecretAsync(keyVaultSecretName, cancellationToken: ct);
        return response.Value.Value;
    }
}
