using Robe.Core.Domain;
using Robe.Infrastructure.Persistence.Azure;

namespace Robe.Api.Tests;

// Exercises AzureCosmosGarmentRepository.BuildListQuery directly (internal, InternalsVisibleTo
// robe.api.tests) — the only part of the real Cosmos-backed repository that's pure logic and
// doesn't require a live Cosmos account. The CRUD/query execution itself is verified against
// the deployed dev stage, the same way AzureOpenAIChatAdapter's real SDK call is (no unit test
// mocks the Cosmos SDK, matching this repo's no-mocking-library convention).
public class AzureCosmosGarmentRepositoryTests
{
    private static (string Name, object Value) Param(Microsoft.Azure.Cosmos.QueryDefinition query, string name) =>
        query.GetQueryParameters().Single(p => p.Name == name);

    [Fact]
    public void BuildListQuery_NoCategoryFilter_OmitsCategoryClauseAndParameter()
    {
        var query = AzureCosmosGarmentRepository.BuildListQuery("user-a", new GarmentQuery(null, 1, 20));

        Assert.DoesNotContain("category", query.QueryText);
        Assert.Contains("WHERE c.userId = @userId", query.QueryText);
        Assert.Contains("ORDER BY c.createdAt OFFSET @offset LIMIT @limit", query.QueryText);
        Assert.DoesNotContain(query.GetQueryParameters(), p => p.Name == "@category");
        Assert.Equal("user-a", Param(query, "@userId").Value);
    }

    [Fact]
    public void BuildListQuery_WithCategoryFilter_IncludesClauseAndCamelCaseParameterValue()
    {
        var query = AzureCosmosGarmentRepository.BuildListQuery(
            "user-a", new GarmentQuery(GarmentCategory.Outerwear, 1, 20));

        Assert.Contains("AND c.traits.category = @category", query.QueryText);
        Assert.Equal("outerwear", Param(query, "@category").Value);
    }

    [Theory]
    [InlineData(1, 20, 0, 20)]
    [InlineData(2, 20, 20, 20)]
    [InlineData(3, 10, 20, 10)]
    public void BuildListQuery_Paging_ComputesOffsetAndLimitFromPageAndPageSize(
        int page, int pageSize, int expectedOffset, int expectedLimit)
    {
        var query = AzureCosmosGarmentRepository.BuildListQuery("user-a", new GarmentQuery(null, page, pageSize));

        Assert.Equal(expectedOffset, Param(query, "@offset").Value);
        Assert.Equal(expectedLimit, Param(query, "@limit").Value);
    }
}
