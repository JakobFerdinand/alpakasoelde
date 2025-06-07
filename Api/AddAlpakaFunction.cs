using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.IO;
using Microsoft.AspNetCore.Http;

namespace Api;

public class AddAlpakaFunction(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<AddAlpakaFunction>();

    private const int NameMaxLength = 100;

    [Function("add-alpaka")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "dashboard/alpakas")] HttpRequestData req)
    {
        IFormCollection form = await req.ReadFormAsync().ConfigureAwait(false);

        string? name = form["name"].FirstOrDefault()?.Trim();
        string? geburtsdatum = form["geburtsdatum"].FirstOrDefault()?.Trim();
        IFormFile? imageFile = form.Files.GetFile("photo");

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

        string? connectionString = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageConnection);

        if (imageFile is not null && imageFile.Length > 0)
        {
            var ext = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
            if (ext is ".png" or ".jpg" or ".jpeg")
            {
                BlobContainerClient containerClient = new(connectionString, "alpakas");
                await containerClient.CreateIfNotExistsAsync().ConfigureAwait(false);
                string blobName = $"{Guid.NewGuid()}{ext}";
                BlobClient blobClient = containerClient.GetBlobClient(blobName);
                await blobClient.UploadAsync(imageFile.OpenReadStream(), new BlobHttpHeaders { ContentType = imageFile.ContentType }).ConfigureAwait(false);
                entity.ImageUrl = blobClient.Uri.ToString();
            }
        }

        TableClient tableClient = new(connectionString, "alpakas");
        await tableClient.AddEntityAsync(entity).ConfigureAwait(false);

        var response = req.CreateResponse(HttpStatusCode.SeeOther);
        response.Headers.Add("Location", "/dashboard");

        return response;
    }
}
