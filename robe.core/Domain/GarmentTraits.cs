namespace Robe.Core.Domain;

public enum GarmentCategory { Top, Bottom, Dress, Outerwear, Footwear, Accessory, Other }
public enum GarmentPattern { Solid, Striped, Plaid, Checked, Floral, Graphic, Other }
public enum GarmentFit { Slim, Regular, Loose, Oversized }
public enum Season { Spring, Summer, Fall, Winter, All }

public class GarmentTraits
{
    public GarmentCategory Category { get; init; }
    public string Subcategory { get; init; } = "";
    public ColorInfo PrimaryColor { get; init; } = null!;
    public List<ColorInfo> SecondaryColors { get; init; } = new List<ColorInfo>();
    public GarmentPattern Pattern { get; init; }
    public string? Material { get; init; }
    public GarmentFit? Fit { get; init; }
    public int Formality { get; init; }
    public List<Season> Seasonality { get; init; } = new List<Season>();
    public List<string> StyleTags { get; init; } = new List<string>();
    public List<string> Occasions { get; init; } = new List<string>();
    public double Confidence { get; init; }
}
