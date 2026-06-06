namespace Robe.Core.Domain;

public class Garment : AuditableEntity
{
    public string Id { get; init; } = "";
    public string UserId { get; init; } = "";
    public GarmentTraits Traits { get; init; } = null!;
    public string ImageUrl { get; init; } = "";
    public string BlobKey { get; init; } = "";
}
