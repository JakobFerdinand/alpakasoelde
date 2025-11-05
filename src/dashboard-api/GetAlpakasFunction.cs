using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.IO;

namespace DashboardApi;

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
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "alpakas")] HttpRequestData req)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("alpakas");
        BlobContainerClient container = _blobServiceClient.GetBlobContainerClient("alpakas");

        var alpakas = tableClient
            .Query<AlpakaEntity>()
            .OrderBy(a => a.Name)
            .Select(a =>
            {
                string? sasUrl = null;
                if (!string.IsNullOrWhiteSpace(a.ImageUrl))
                {
                    string blobName = Path.GetFileName(new Uri(a.ImageUrl).AbsolutePath);
                    BlobClient blob = container.GetBlobClient(blobName);

                    var expiresOn = DateTimeOffset.UtcNow.AddMinutes(30);
                    var sasBuilder = new BlobSasBuilder
                    {
                        BlobContainerName = container.Name,
                        BlobName = blobName,
                        Resource = "b",
                        ExpiresOn = expiresOn
                    };
                    sasBuilder.SetPermissions(BlobSasPermissions.Read);

                    var storageAccountName = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageAccountName)
                        ?? throw new InvalidOperationException("Environment variable 'AZURE_STORAGE_ACCOUNT_NAME' is not set.");
                    var storageAccountKey = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageAccountKey)
                        ?? throw new InvalidOperationException("Environment variable 'AZURE_STORAGE_ACCOUNT_KEY' is not set.");
                    StorageSharedKeyCredential credential = new(
                        storageAccountName,
                        storageAccountKey
                    );
                    var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
                    sasUrl = $"{blob.Uri}?{sasToken}";
                }

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
