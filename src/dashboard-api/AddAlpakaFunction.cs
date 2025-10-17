using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using HttpMultipartParser;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace DashboardApi;

public class AddAlpakaFunction(
    ILoggerFactory loggerFactory,
    TableServiceClient tableServiceClient,
    BlobServiceClient blobServiceClient)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<AddAlpakaFunction>();
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;
    private readonly BlobServiceClient _blobServiceClient = blobServiceClient;

    private const int NameMaxLength = 100;
    private const long MaxImageSizeBytes = 15 * 1024 * 1024; // 15 MB

    [Function("add-alpaka")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "alpakas")] HttpRequestData req)
    {
        var parsedFormBocy = await MultipartFormDataParser.ParseAsync(req.Body).ConfigureAwait(false);

        string? name = parsedFormBocy.GetParameterValue("name").Trim();
        string? geburtsdatum = parsedFormBocy.GetParameterValue("geburtsdatum").Trim();
        FilePart? imageFile = parsedFormBocy.Files.FirstOrDefault();

        _logger.LogInformation("Parsed form data - Name: '{Name}', Geburtsdatum: '{Geburtsdatum}', HasImage: {HasImage}", name, geburtsdatum, imageFile is not null);

        var missingFields = new[]
            {
                (Value: name, FieldName: "Name"),
                (Value: geburtsdatum, FieldName: "Geburtsdatum")
            }
            .Where(x => string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.FieldName)
            .ToList();
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

        if (name!.Length > NameMaxLength)
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

        AlpakaEntity entity = new()
        {
            Name = name!,
            Geburtsdatum = geburtsdatum!
        };


        if (imageFile is not null && imageFile.Data.Length > 0)
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
                BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient("alpakas");
                await containerClient.CreateIfNotExistsAsync().ConfigureAwait(false);
                string blobName = $"{Guid.NewGuid()}{ext}";
                BlobClient blobClient = containerClient.GetBlobClient(blobName);
                var uploadResponse = await blobClient.UploadAsync(imageFile.Data, new BlobHttpHeaders { ContentType = imageFile.ContentType }).ConfigureAwait(false);
                entity.ImageUrl = blobClient.Uri.ToString();
            }
            else
            {
                _logger.LogWarning("Unsupported image file type: {FileName} with extension {Extension}. Only .png, .jpg or .jpeg is supported.", imageFile.FileName, ext);
            }
        }

        TableClient tableClient = _tableServiceClient.GetTableClient("alpakas");
        await tableClient.AddEntityAsync(entity).ConfigureAwait(false);

        var response = req.CreateResponse(HttpStatusCode.SeeOther);
        response.Headers.Add("Location", "/dashboard");

        return response;
    }
}
