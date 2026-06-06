using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Secrets;

// Stub — wire up Azure.Security.KeyVault.Secrets + Azure.Identity when deploying to cloud.
public class AzureKeyVaultSecretManager : ISecretManager
{
    public Task<string> GetSecretAsync(string name, CancellationToken ct = default) =>
        throw new NotImplementedException(
            "AzureKeyVaultSecretManager requires cloud deployment. Use MockSecretManager locally.");
}
