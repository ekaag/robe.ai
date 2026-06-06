namespace Robe.Core.Domain;

public class Recommendation
{
    public InventoryItem InventoryItem { get; init; } = null!;
    public double Score { get; init; }
    public string Reasoning { get; init; } = "";
}
