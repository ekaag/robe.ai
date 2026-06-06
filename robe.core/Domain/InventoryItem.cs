namespace Robe.Core.Domain;

public class InventoryItem : AuditableEntity
{
    public string Id { get; init; } = "";
    public string VendorId { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public GarmentTraits Traits { get; init; } = null!;
    public decimal Price { get; init; }
    public string Currency { get; init; } = "USD";
    public string Url { get; init; } = "";
    public string ImageUrl { get; init; } = "";
    public bool InStock { get; init; }
}

public record InventoryQuery(
    IReadOnlyList<GarmentCategory>? Categories = null,
    string? Occasion = null,
    decimal? MaxPrice = null,
    string? Currency = null,
    bool InStockOnly = true);
