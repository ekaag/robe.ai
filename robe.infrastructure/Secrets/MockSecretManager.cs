using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Secrets;

public class MockSecretManager : ISecretManager
{
    private readonly Dictionary<string, string> _secrets;

    public MockSecretManager(Dictionary<string, string> secrets) => _secrets = secrets;

    public Task<string> GetSecretAsync(string name, CancellationToken ct = default) =>
        _secrets.TryGetValue(name, out var value)
            ? Task.FromResult(value)
            : throw new KeyNotFoundException($"Secret '{name}' not found in MockSecretManager.");
}
