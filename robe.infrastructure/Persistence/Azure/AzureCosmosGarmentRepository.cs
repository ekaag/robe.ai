using System.Net;
using System.Text.Json;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Robe.Core.Domain;
using Robe.Core.Interfaces;

namespace Robe.Infrastructure.Persistence.Azure;

/// <summary>
/// Cosmos DB (SQL API) implementation of <see cref="IGarmentRepository"/>. The container is
/// partitioned on <c>/userId</c> — every operation here scopes by userId, so all reads/writes
/// (including <see cref="ListAsync"/>'s query) stay within a single partition.
///
/// Endpoint/database/container names are fetched from <see cref="ISecretManager"/> (Key Vault
/// in production) on first use and cached for the lifetime of this singleton. Auth is via
/// <see cref="DefaultAzureCredential"/> (Managed Identity in Azure, granted "Cosmos DB Built-in
/// Data Contributor" in stage.bicep) — no connection string or key is stored anywhere.
/// </summary>
public class AzureCosmosGarmentRepository : IGarmentRepository
{
    private readonly ISecretManager _secrets;
    private Container? _container;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    public AzureCosmosGarmentRepository(ISecretManager secrets)
    {
        _secrets = secrets;
    }

    private async Task<Container> GetContainerAsync(CancellationToken ct)
    {
        if (_container is not null) return _container;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_container is not null) return _container;
            var endpoint = (await _secrets.GetSecretAsync("CosmosDb:Endpoint", ct)).Trim();
            var databaseName = (await _secrets.GetSecretAsync("CosmosDb:DatabaseName", ct)).Trim();
            var containerName = (await _secrets.GetSecretAsync("CosmosDb:ContainerName", ct)).Trim();

            var client = new CosmosClient(endpoint, new DefaultAzureCredential(), new CosmosClientOptions
            {
                Serializer = new CosmosJsonSerializer()
            });
            _container = client.GetContainer(databaseName, containerName);
            return _container;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<Garment> AddAsync(Garment garment, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        var response = await container.CreateItemAsync(garment, new PartitionKey(garment.UserId), cancellationToken: ct);
        return response.Resource;
    }

    public async Task<Garment?> GetByIdAsync(string id, string userId, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        try
        {
            var response = await container.ReadItemAsync<Garment>(id, new PartitionKey(userId), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<Garment>> ListAsync(string userId, GarmentQuery query, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        var queryDefinition = BuildListQuery(userId, query);
        var requestOptions = new QueryRequestOptions { PartitionKey = new PartitionKey(userId) };

        var results = new List<Garment>();
        using var iterator = container.GetItemQueryIterator<Garment>(queryDefinition, requestOptions: requestOptions);
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(ct);
            results.AddRange(page);
        }
        return results;
    }

    public async Task<bool> DeleteAsync(string id, string userId, CancellationToken ct = default)
    {
        var container = await GetContainerAsync(ct);
        try
        {
            await container.DeleteItemAsync<Garment>(id, new PartitionKey(userId), cancellationToken: ct);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    // Internal (InternalsVisibleTo robe.api.tests) so the query shape — category filter,
    // OFFSET/LIMIT paging — is verified without needing a live Cosmos connection.
    internal static QueryDefinition BuildListQuery(string userId, GarmentQuery query)
    {
        var sql = "SELECT * FROM c WHERE c.userId = @userId";
        if (query.Category is not null)
            sql += " AND c.traits.category = @category";
        sql += " ORDER BY c.createdAt OFFSET @offset LIMIT @limit";

        var definition = new QueryDefinition(sql)
            .WithParameter("@userId", userId)
            .WithParameter("@offset", (query.Page - 1) * query.PageSize)
            .WithParameter("@limit", query.PageSize);

        if (query.Category is not null)
        {
            // Matches the camelCase enum string CosmosJsonSerializer stores (GarmentCategory.Top -> "top").
            var categoryValue = JsonNamingPolicy.CamelCase.ConvertName(query.Category.Value.ToString());
            definition = definition.WithParameter("@category", categoryValue);
        }

        return definition;
    }
}
