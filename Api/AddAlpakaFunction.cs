using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Web;

namespace Api;

public class AddAlpakaFunction(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<AddAlpakaFunction>();

    private const int NameMaxLength = 100;

    [Function("add-alpaka")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "dashboard/alpakas")] HttpRequestData req)
    {
        string? body = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);

        _logger.LogInformation("Received alpaka data: {Body}", body);

        var parsedForm = HttpUtility.ParseQueryString(body);
        string? name = parsedForm["name"]?.Trim();
        string? geburtsdatum = parsedForm["geburtsdatum"]?.Trim();
        _logger.LogInformation("Parsed form data - Name: '{Name}', Geburtsdatum: '{Geburtsdatum}'", name, geburtsdatum);

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
        TableClient tableClient = new(connectionString, "alpakas");
        await tableClient.AddEntityAsync(entity).ConfigureAwait(false);

        var response = req.CreateResponse(HttpStatusCode.SeeOther);
        response.Headers.Add("Location", "/dashboard");

        return response;
    }
}
