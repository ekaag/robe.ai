namespace Robe.Infrastructure.TraitsExtraction.Azure;

public sealed class AzureOpenAIOptions
{
    /// <summary>Maximum number of images accepted per <see cref="IFashionTraitsExtractor"/> call.</summary>
    public int MaxImages { get; init; } = 10;

    /// <summary>Maximum image size in bytes (default 20 MB).</summary>
    public long MaxImageSizeBytes { get; init; } = 20L * 1024 * 1024;
}
