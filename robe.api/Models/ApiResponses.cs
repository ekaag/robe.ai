using Robe.Core.Domain;

namespace Robe.Api.Models;

public record AnalyzeResponse(GarmentTraits Traits, string ModelVersion);

public record GarmentResponse(
    string Id,
    string UserId,
    GarmentTraits Traits,
    string ImageUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    string CreatedByUserId,
    string ModifiedByUserId);

public record GarmentListResponse(
    IEnumerable<GarmentResponse> Items,
    int Page,
    int PageSize);

public record ProfileResponse(
    List<string> DominantStyles,
    List<ColorWeight> ColorPalette,
    FormalityRange FormalityRange,
    List<string> PreferredFits,
    Dictionary<string, double> SeasonalSkew,
    string Summary,
    int GarmentCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset ModifiedAt,
    string CreatedByUserId,
    string ModifiedByUserId);

public record RecommendationsResponse(
    IEnumerable<Recommendation> Recommendations,
    DateTimeOffset ProfileGeneratedAt,
    DateTimeOffset RequestedAt);

public record ErrorResponse(string Error, string? Detail = null);
