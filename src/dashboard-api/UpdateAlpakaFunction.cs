using Azure;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using HttpMultipartParser;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.IO;

namespace DashboardApi;

public class UpdateAlpakaFunction(
        ILoggerFactory loggerFactory,
        TableServiceClient tableServiceClient,
        BlobServiceClient blobServiceClient)
{
        private readonly ILogger _logger = loggerFactory.CreateLogger<UpdateAlpakaFunction>();
        private readonly TableServiceClient _tableServiceClient = tableServiceClient;
        private readonly BlobServiceClient _blobServiceClient = blobServiceClient;

        private const int NameMaxLength = 100;
        private const long MaxImageSizeBytes = 15 * 1024 * 1024; // 15 MB

        [Function("update-alpaka")]
        public async Task<HttpResponseData> Run(
                [HttpTrigger(AuthorizationLevel.Function, "put", Route = "alpakas/{alpakaId}")] HttpRequestData req,
                string alpakaId)
        {
                if (string.IsNullOrWhiteSpace(alpakaId))
                {
                        var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badRequest.WriteStringAsync("Alpaka id is required.").ConfigureAwait(false);
                        return badRequest;
                }

                var parsedFormBody = await MultipartFormDataParser.ParseAsync(req.Body).ConfigureAwait(false);

                string? name = parsedFormBody.GetParameterValue("name")?.Trim();
                string? geburtsdatum = parsedFormBody.GetParameterValue("geburtsdatum")?.Trim();
                FilePart? imageFile = parsedFormBody.Files.FirstOrDefault();

                List<(string? Value, string FieldName)> requiredFields = [
                        (Value: name, FieldName: "Name"),
                        (Value: geburtsdatum, FieldName: "Geburtsdatum")
                ];
                List<string> missingFields = [.. requiredFields
                        .Where(x => string.IsNullOrWhiteSpace(x.Value))
                        .Select(x => x.FieldName)];
                if (missingFields.Count > 0)
                {
                        var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badRequestResponse.WriteAsJsonAsync(new
                        {
                                title = "Bad Request",
                                status = (int)HttpStatusCode.BadRequest,
                                detail = $"{string.Join(", ", missingFields)} are required fields and must be provided."
                        }).ConfigureAwait(false);
                        return badRequestResponse;
                }

                if (name is { Length: > NameMaxLength })
                {
                        var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                        await badRequestResponse.WriteAsJsonAsync(new
                        {
                                title = "Bad Request",
                                status = (int)HttpStatusCode.BadRequest,
                                detail = $"Name exceeds {NameMaxLength} characters."
                        }).ConfigureAwait(false);
                        return badRequestResponse;
                }

                TableClient tableClient = _tableServiceClient.GetTableClient("alpakas");
                BlobContainerClient container = _blobServiceClient.GetBlobContainerClient("alpakas");

                try
                {
                        var existingResponse = await tableClient.GetEntityAsync<AlpakaEntity>("AlpakaPartition", alpakaId)
                                .ConfigureAwait(false);
                        AlpakaEntity alpaka = existingResponse.Value;

                        alpaka.Name = name!;
                        alpaka.Geburtsdatum = geburtsdatum!;

                        if (imageFile is { Data.Length: > 0 })
                        {
                                if (imageFile.Data.Length > MaxImageSizeBytes)
                                {
                                        _logger.LogWarning("Image file size {FileSize} exceeds maximum allowed size of {MaxFileSize} bytes.", imageFile.Data.Length, MaxImageSizeBytes);
                                        var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                                        await badRequestResponse.WriteAsJsonAsync(new
                                        {
                                                title = "Bad Request",
                                                status = (int)HttpStatusCode.BadRequest,
                                                detail = $"Image file exceeds the maximum allowed size of {MaxImageSizeBytes / (1024 * 1024)}MB."
                                        }).ConfigureAwait(false);
                                        return badRequestResponse;
                                }

                                var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                                if (ext is ".png" or ".jpg" or ".jpeg")
                                {
                                        await container.CreateIfNotExistsAsync().ConfigureAwait(false);
                                        string blobName = $"{Guid.NewGuid()}{ext}";
                                        BlobClient blobClient = container.GetBlobClient(blobName);
                                        await blobClient.UploadAsync(imageFile.Data, new BlobHttpHeaders { ContentType = imageFile.ContentType }).ConfigureAwait(false);

                                        if (!string.IsNullOrWhiteSpace(alpaka.ImageUrl))
                                        {
                                                string existingBlobName = Path.GetFileName(new Uri(alpaka.ImageUrl).AbsolutePath);
                                                BlobClient oldBlob = container.GetBlobClient(existingBlobName);
                                                try
                                                {
                                                        await oldBlob.DeleteIfExistsAsync().ConfigureAwait(false);
                                                }
                                                catch (RequestFailedException ex)
                                                {
                                                        _logger.LogWarning(ex, "Failed to delete old alpaka image blob {BlobName}.", existingBlobName);
                                                }
                                        }

                                        alpaka.ImageUrl = blobClient.Uri.ToString();
                                }
                                else
                                {
                                        _logger.LogWarning("Unsupported image file type: {FileName} with extension {Extension}. Only .png, .jpg or .jpeg is supported.", imageFile.FileName, ext);
                                        var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                                        await badRequestResponse.WriteAsJsonAsync(new
                                        {
                                                title = "Bad Request",
                                                status = (int)HttpStatusCode.BadRequest,
                                                detail = "Unsupported image file type. Only .png, .jpg or .jpeg is allowed."
                                        }).ConfigureAwait(false);
                                        return badRequestResponse;
                                }
                        }

                        await tableClient.UpdateEntityAsync(alpaka, existingResponse.Value.ETag, TableUpdateMode.Replace)
                                .ConfigureAwait(false);

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
                        _logger.LogError(ex, "Failed to update alpaka with id {AlpakaId}", alpakaId);
                        var failure = req.CreateResponse(HttpStatusCode.InternalServerError);
                        await failure.WriteStringAsync("Failed to update alpaka.").ConfigureAwait(false);
                        return failure;
                }
        }
}
