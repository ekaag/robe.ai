namespace Robe.Core.Interfaces;

public interface ISecretManager
{
    Task<string> GetSecretAsync(string name, CancellationToken ct = default);
}
