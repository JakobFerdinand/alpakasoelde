using Azure;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Api;

public class GetAlpakaFunction(
    ILoggerFactory loggerFactory,
    TableServiceClient tableServiceClient,
    BlobServiceClient blobServiceClient)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GetAlpakaFunction>();
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;

    [Function("get-alpaka")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dashboard/alpakas/{id}")] HttpRequestData req,
        string id)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("alpakas");
        BlobContainerClient container = _blobServiceClient.GetBlobContainerClient("alpakas");

        try
        {
            Response<AlpakaEntity> result = await tableClient.GetEntityAsync<AlpakaEntity>("AlpakaPartition", id).ConfigureAwait(false);
            AlpakaEntity alpaka = result.Value;

            var storageAccountName = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageAccountName)
                ?? throw new InvalidOperationException("Environment variable 'AZURE_STORAGE_ACCOUNT_NAME' is not set.");
            var storageAccountKey = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageAccountKey)
                ?? throw new InvalidOperationException("Environment variable 'AZURE_STORAGE_ACCOUNT_KEY' is not set.");

            string? sasUrl = SasTokenHelper.GenerateSasUrl(container, alpaka.ImageUrl, storageAccountName, storageAccountKey);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                Id = alpaka.RowKey,
                alpaka.Name,
                alpaka.Geburtsdatum,
                ImageUrl = sasUrl
            }).ConfigureAwait(false);
            return response;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            var response = req.CreateResponse(HttpStatusCode.NotFound);
            await response.WriteAsJsonAsync(new { detail = "Not Found" }).ConfigureAwait(false);
            return response;
        }
    }
}
