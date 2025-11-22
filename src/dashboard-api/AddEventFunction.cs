using System.Net;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi;

public class AddEventFunction(
    ILoggerFactory loggerFactory,
    TableServiceClient tableServiceClient)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<AddEventFunction>();
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;

    private const int MaxEventTypeLength = 100;
    private const int MaxCommentLength = 1000;

    [Function("add-event")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "events")] HttpRequestData req)
    {
        AddEventRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<AddEventRequest>(req.Body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON payload for add-event.");
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                title = "Bad Request",
                status = (int)HttpStatusCode.BadRequest,
                detail = "Ungültiger Anfrageinhalt."
            }).ConfigureAwait(false);
            return badRequest;
        }

        if (payload is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                title = "Bad Request",
                status = (int)HttpStatusCode.BadRequest,
                detail = "Ein Ereignis muss angegeben werden."
            }).ConfigureAwait(false);
            return badRequest;
        }

        string? eventType = payload.EventType?.Trim();
        List<string> alpakaIds = payload.AlpakaIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
        string? comment = payload.Comment?.Trim();

        if (string.IsNullOrWhiteSpace(eventType))
        {
            return await CreateValidationError(req, "Das Ereignisfeld ist erforderlich.").ConfigureAwait(false);
        }

        if (eventType.Length > MaxEventTypeLength)
        {
            return await CreateValidationError(req, $"Der Ereignistyp darf maximal {MaxEventTypeLength} Zeichen lang sein.").ConfigureAwait(false);
        }

        if (alpakaIds.Count == 0)
        {
            return await CreateValidationError(req, "Mindestens ein Alpaka muss ausgewählt werden.").ConfigureAwait(false);
        }

        if (comment is { Length: > MaxCommentLength })
        {
            return await CreateValidationError(req, $"Die Notiz darf maximal {MaxCommentLength} Zeichen enthalten.").ConfigureAwait(false);
        }

        if (!DateTime.TryParse(payload.EventDate, out DateTime parsedDate))
        {
            return await CreateValidationError(req, "Das Datum ist ungültig.").ConfigureAwait(false);
        }

        if (payload.Cost is < 0)
        {
            return await CreateValidationError(req, "Kosten dürfen nicht negativ sein.").ConfigureAwait(false);
        }

        TableClient tableClient = _tableServiceClient.GetTableClient("events");
        await tableClient.CreateIfNotExistsAsync().ConfigureAwait(false);

        string sharedEventId = Guid.NewGuid().ToString();

        foreach (string alpakaId in alpakaIds)
        {
            EventEntity entity = new()
            {
                EventType = eventType!,
                EventDate = parsedDate,
                Comment = comment,
                Cost = payload.Cost,
                PartitionKey = alpakaId,
                RowKey = sharedEventId,
                SharedEventId = sharedEventId
            };

            await tableClient.AddEntityAsync(entity).ConfigureAwait(false);
        }

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new { id = sharedEventId }).ConfigureAwait(false);
        return response;
    }

    private static async Task<HttpResponseData> CreateValidationError(HttpRequestData req, string message)
    {
        var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
        await badRequestResponse.WriteAsJsonAsync(new
        {
            title = "Bad Request",
            status = (int)HttpStatusCode.BadRequest,
            detail = message
        }).ConfigureAwait(false);
        return badRequestResponse;
    }
}

public sealed record AddEventRequest
{
    public string? EventType { get; init; }
    public List<string>? AlpakaIds { get; init; }
    public string? EventDate { get; init; }
    public double? Cost { get; init; }
    public string? Comment { get; init; }
}
