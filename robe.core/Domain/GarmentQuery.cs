namespace Robe.Core.Domain;

public record GarmentQuery(GarmentCategory? Category = null, int Page = 1, int PageSize = 20);
