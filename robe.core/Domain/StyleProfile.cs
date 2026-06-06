namespace Robe.Core.Domain;

public class StyleProfile : AuditableEntity
{
    public List<string> DominantStyles { get; init; } = new List<string>();
    public List<ColorWeight> ColorPalette { get; init; } = new List<ColorWeight>();
    public FormalityRange FormalityRange { get; init; } = new FormalityRange(0, 0, 0);
    public List<string> PreferredFits { get; init; } = new List<string>();
    public Dictionary<string, double> SeasonalSkew { get; init; } = new Dictionary<string, double>();
    public string Summary { get; init; } = "";
    public int GarmentCount { get; init; }
}

public record ColorWeight(string Name, string Hex, double Weight);

public record FormalityRange(int Min, int Max, double Typical);
