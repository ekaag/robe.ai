using Robe.Core.Domain;

namespace Robe.Core.Interfaces;

public interface IImageStore
{
    Task<string> SaveAsync(ImageInput image, string key, CancellationToken ct = default);
    Task DeleteAsync(string key, CancellationToken ct = default);
}
