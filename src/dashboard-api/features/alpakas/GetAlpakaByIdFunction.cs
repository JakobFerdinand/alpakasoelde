using Azure;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.IO;
using DashboardApi.Shared;

namespace DashboardApi.Features.Alpakas;

public class GetAlpakaByIdFunction(
    ILoggerFactory loggerFactory,
    TableServiceClient tableServiceClient,
    BlobServiceClient blobServiceClient)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GetAlpakaByIdFunction>();
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;

    [Function("get-alpaka-by-id")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "alpakas/{alpakaId}")] HttpRequestData req,
        string alpakaId)
    {
        if (string.IsNullOrWhiteSpace(alpakaId))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteStringAsync("Alpaka id is required.").ConfigureAwait(false);
            return badRequest;
        }

        TableClient tableClient = _tableServiceClient.GetTableClient("alpakas");
        BlobContainerClient container = _blobServiceClient.GetBlobContainerClient("alpakas");

        try
        {
            var entityResponse = await tableClient.GetEntityAsync<AlpakaEntity>("AlpakaPartition", alpakaId)
                .ConfigureAwait(false);
            AlpakaEntity alpaka = entityResponse.Value;

            string? sasUrl = null;
            if (!string.IsNullOrWhiteSpace(alpaka.ImageUrl))
            {
                string blobName = Path.GetFileName(new Uri(alpaka.ImageUrl).AbsolutePath);
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

                StorageSharedKeyCredential credential = new(storageAccountName, storageAccountKey);
                var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
                sasUrl = $"{blob.Uri}?{sasToken}";
            }

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
            _logger.LogInformation("Alpaka with id {AlpakaId} not found.", alpakaId);
            var notFound = req.CreateResponse(HttpStatusCode.NotFound);
            await notFound.WriteStringAsync("Alpaka not found.").ConfigureAwait(false);
            return notFound;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve alpaka with id {AlpakaId}", alpakaId);
            var failure = req.CreateResponse(HttpStatusCode.InternalServerError);
            await failure.WriteStringAsync("Failed to retrieve alpaka.").ConfigureAwait(false);
            return failure;
        }
    }
}
