using Azure;
using Azure.Data.Tables;
using dashboard_api.shared.entities;

namespace DashboardApi.Features.Gutscheine;

public interface IGutscheinStore
{
    Task<IReadOnlyList<GutscheinEntity>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(GutscheinEntity entity, CancellationToken cancellationToken);
    Task<GutscheinEntity?> GetByGutscheinnummerAsync(string gutscheinnummer, CancellationToken cancellationToken);
    Task UpdateAsync(GutscheinEntity entity, CancellationToken cancellationToken);
}

public sealed class TableGutscheinStore(TableServiceClient tableServiceClient) : IGutscheinStore
{
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;

    public Task<IReadOnlyList<GutscheinEntity>> GetAllAsync(CancellationToken cancellationToken)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("gutscheine");
        var vouchers = tableClient
            .Query<GutscheinEntity>()
            .ToList();
        return Task.FromResult<IReadOnlyList<GutscheinEntity>>(vouchers);
    }

    public async Task AddAsync(GutscheinEntity entity, CancellationToken cancellationToken)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("gutscheine");
        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        await tableClient.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GutscheinEntity?> GetByGutscheinnummerAsync(string gutscheinnummer, CancellationToken cancellationToken)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("gutscheine");

        try
        {
            var response = await tableClient.GetEntityAsync<GutscheinEntity>("GutscheinePartition", gutscheinnummer, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            return response.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task UpdateAsync(GutscheinEntity entity, CancellationToken cancellationToken)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("gutscheine");
        await tableClient.UpdateEntityAsync(entity, ETag.All, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
    }
}
