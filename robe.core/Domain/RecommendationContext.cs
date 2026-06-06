namespace Robe.Core.Domain;

public class RecommendationContext
{
    public decimal? MaxBudget { get; init; }
    public string Currency { get; init; } = "USD";
    public List<GarmentCategory> Categories { get; init; } = new List<GarmentCategory>();
    public string? Occasion { get; init; }
    public int Count { get; init; } = 5;
}
