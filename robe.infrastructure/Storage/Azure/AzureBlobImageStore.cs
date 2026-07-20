using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Storage.Azure;

/// <summary>
/// Azure Blob Storage implementation of <see cref="IImageStore"/>. The container
/// (<c>garment-images</c>, provisioned in stage.bicep) has blob-level public read access, so
/// the URL returned by <see cref="SaveAsync"/> is a stable, permanently-valid link — it's what
/// gets persisted forever in <c>Garment.ImageUrl</c>, so it can't depend on an expiring SAS.
///
/// Blob service URI/container name are fetched from <see cref="ISecretManager"/> (Key Vault in
/// production) on first use and cached for the lifetime of this singleton. Auth for the actual
/// upload/delete calls is via <see cref="DefaultAzureCredential"/> (Managed Identity in Azure,
/// granted "Storage Blob Data Contributor" in stage.bicep) — no connection string or key.
/// </summary>
public class AzureBlobImageStore : IImageStore
{
    private readonly ISecretManager _secrets;
    private BlobContainerClient? _container;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public AzureBlobImageStore(ISecretManager secrets)
    {
        _secrets = secrets;
    }

    private async Task<BlobContainerClient> GetContainerAsync(CancellationToken ct)
    {
        if (_container is not null) return _container;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_container is not null) return _container;
            var blobServiceUri = (await _secrets.GetSecretAsync("Storage:BlobServiceUri", ct)).Trim();
            var containerName = (await _secrets.GetSecretAsync("Storage:ContainerName", ct)).Trim();

            var serviceClient = new BlobServiceClient(new Uri(blobServiceUri), new DefaultAzureCredential());
            _container = serviceClient.GetBlobContainerClient(containerName);
            return _container;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<string> SaveAsync(ImageInput image, string key, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        var blob = container.GetBlobClient(key);

        using var stream = new MemoryStream(image.Data);
        await blob.UploadAsync(stream, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = image.MimeType }
        }, ct);

        return blob.Uri.ToString();
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        await container.GetBlobClient(key).DeleteIfExistsAsync(cancellationToken: ct);
    }
}
