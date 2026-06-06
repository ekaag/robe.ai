using System.Collections.Concurrent;
using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Storage;

public class InMemoryImageStore : IImageStore
{
    private readonly ConcurrentDictionary<string, bool> _keys = new();

    public Task<string> SaveAsync(ImageInput image, string key, CancellationToken ct = default)
    {
        _keys[key] = true;
        return Task.FromResult($"https://inmemory.test/{key}");
    }

    public Task DeleteAsync(string key, CancellationToken ct = default)
    {
        _keys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public bool HasKey(string key) => _keys.ContainsKey(key);
}
