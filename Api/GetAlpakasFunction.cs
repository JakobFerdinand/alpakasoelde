using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.IO;

namespace Api;

public class GetAlpakasFunction(
    ILoggerFactory loggerFactory,
    TableServiceClient tableServiceClient,
    BlobServiceClient blobServiceClient)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GetAlpakasFunction>();
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;

    [Function("get-alpakas")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dashboard/alpakas")] HttpRequestData req)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("alpakas");
        BlobContainerClient container = _blobServiceClient.GetBlobContainerClient("alpakas");

        var storageAccountName = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageAccountName)
            ?? throw new InvalidOperationException("Environment variable 'AZURE_STORAGE_ACCOUNT_NAME' is not set.");
        var storageAccountKey = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageAccountKey)
            ?? throw new InvalidOperationException("Environment variable 'AZURE_STORAGE_ACCOUNT_KEY' is not set.");

        var alpakas = tableClient
            .Query<AlpakaEntity>()
            .OrderBy(a => a.Name)
            .Select(a =>
            {
                string? sasUrl = SasTokenHelper.GenerateSasUrl(container, a.ImageUrl, storageAccountName, storageAccountKey);

                return new
                {
                    Id = a.RowKey,
                    a.Name,
                    a.Geburtsdatum,
                    ImageUrl = sasUrl
                };
            })
            .ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(alpakas).ConfigureAwait(false);
        return response;
    }
}
