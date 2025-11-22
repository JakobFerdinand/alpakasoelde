using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi;

public class GetAlpakaEventsFunction(
    ILoggerFactory loggerFactory,
    TableServiceClient tableServiceClient)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GetAlpakaEventsFunction>();
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;

    [Function("get-alpaka-events")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "alpakas/{alpakaId}/events")] HttpRequestData req,
        string alpakaId)
    {
        if (string.IsNullOrWhiteSpace(alpakaId))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                title = "Bad Request",
                status = (int)HttpStatusCode.BadRequest,
                detail = "Eine Alpaka-ID ist erforderlich."
            }).ConfigureAwait(false);
            return badRequest;
        }

        TableClient tableClient = _tableServiceClient.GetTableClient("events");
        await tableClient.CreateIfNotExistsAsync().ConfigureAwait(false);

        var events = tableClient
            .Query<EventEntity>(e => e.PartitionKey == alpakaId)
            .OrderByDescending(e => e.EventDate)
            .Select(e => new
            {
                id = e.RowKey,
                eventType = e.EventType,
                eventDate = e.EventDate.ToString("yyyy-MM-dd"),
                comment = e.Comment,
                cost = e.Cost
            })
            .ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(events).ConfigureAwait(false);
        return response;
    }
}
