using System.Net;
using System.Net.Http;
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

    [Function("events")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "events")] HttpRequestData req)
    {
        if (string.Equals(req.Method, HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase))
        {
            return await GetEventsAsync(req).ConfigureAwait(false);
        }

        if (!string.Equals(req.Method, HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase))
        {
            var methodNotAllowed = req.CreateResponse(HttpStatusCode.MethodNotAllowed);
            await methodNotAllowed.WriteAsJsonAsync(new
            {
                title = "Method Not Allowed",
                status = (int)HttpStatusCode.MethodNotAllowed
            }).ConfigureAwait(false);
            return methodNotAllowed;
        }

        return await AddEventAsync(req).ConfigureAwait(false);
    }

    private async Task<HttpResponseData> GetEventsAsync(HttpRequestData req)
    {
        TableClient eventsTableClient = _tableServiceClient.GetTableClient("events");
        await eventsTableClient.CreateIfNotExistsAsync().ConfigureAwait(false);

        TableClient alpakaTableClient = _tableServiceClient.GetTableClient("alpakas");
        await alpakaTableClient.CreateIfNotExistsAsync().ConfigureAwait(false);

        Dictionary<string, string> alpakaLookup = alpakaTableClient
            .Query<AlpakaEntity>()
            .ToDictionary(a => a.RowKey, a => a.Name, StringComparer.OrdinalIgnoreCase);

        var events = eventsTableClient
            .Query<EventEntity>()
            .ToList()
            .GroupBy(e => string.IsNullOrWhiteSpace(e.SharedEventId) ? e.RowKey : e.SharedEventId)
            .Select(group =>
            {
                EventEntity first = group.First();
                List<string> alpakaIds = group
                    .Select(e => e.PartitionKey)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                List<string> alpakaNames = alpakaIds
                    .Select(id => alpakaLookup.TryGetValue(id, out string? name) ? name : null)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .ToList();

                return new
                {
                    id = string.IsNullOrWhiteSpace(first.SharedEventId) ? first.RowKey : first.SharedEventId,
                    first.EventType,
                    first.EventDate,
                    first.Comment,
                    first.Cost,
                    AlpakaIds = alpakaIds,
                    AlpakaNames = alpakaNames
                };
            })
            .OrderByDescending(e => e.EventDate)
            .ThenByDescending(e => e.id)
            .Select(e => new
            {
                e.id,
                eventType = e.EventType,
                eventDate = e.EventDate.ToString("yyyy-MM-dd"),
                e.Comment,
                e.Cost,
                e.AlpakaIds,
                e.AlpakaNames
            })
            .ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(events).ConfigureAwait(false);
        return response;
    }

    private async Task<HttpResponseData> AddEventAsync(HttpRequestData req)
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

        if (!DateTimeOffset.TryParse(payload.EventDate, out DateTimeOffset parsedDate))
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
                EventDate = new DateTimeOffset(parsedDate.Date, TimeSpan.Zero),
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
