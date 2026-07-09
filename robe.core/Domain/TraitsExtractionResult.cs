namespace Robe.Core.Domain;

public sealed record TraitsExtractionResult(
    IReadOnlyList<ImageTraitsResult> Images);

public sealed record ImageTraitsResult(
    string ImageId,
    IReadOnlyList<PersonTraits> People,
    IReadOnlyList<string> Warnings);

public sealed record PersonTraits(
    string PersonId,
    string? Position,
    IReadOnlyList<string> OverallStyle,
    IReadOnlyList<string> StyleTags,
    IReadOnlyList<ClothingItemTraits> ClothingItems,
    double OverallConfidence);

public sealed record ClothingItemTraits(
    string Category,
    string Type,
    string? Subtype,
    ColorTraits? PrimaryColor,
    IReadOnlyList<ColorTraits> SecondaryColors,
    string? Pattern,
    string? Material,
    string? Fit,
    string? Length,
    string? SleeveLength,
    string? Neckline,
    string? CollarType,
    string? WaistRise,
    string? ClosureType,
    IReadOnlyList<string> Details,
    string? VisibleText,
    string? Brand,
    string? Logo,
    string? Condition,
    IReadOnlyList<string> StyleTags,
    double Confidence);

public sealed record ColorTraits(string Normalized, string? Shade);
