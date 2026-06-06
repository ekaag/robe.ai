using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Storage;

// Stub — wire up Azure.Storage.Blobs when deploying to cloud.
public class AzureBlobImageStore : IImageStore
{
    public Task<string> SaveAsync(ImageInput image, string key, CancellationToken ct = default) =>
        throw new NotImplementedException("AzureBlobImageStore requires Azure Storage configuration.");

    public Task DeleteAsync(string key, CancellationToken ct = default) =>
        throw new NotImplementedException("AzureBlobImageStore requires Azure Storage configuration.");
}
