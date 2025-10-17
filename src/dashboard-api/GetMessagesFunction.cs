using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace DashboardApi;

public class GetMessagesFunction(ILoggerFactory loggerFactory, TableServiceClient tableServiceClient)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GetMessagesFunction>();
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;

    [Function("get-messages")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "messages")] HttpRequestData req)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("messages");

        var messages = tableClient
            .Query<MessageEntity>()
            .OrderByDescending(m => m.Timestamp)
            .Select(m => new
            {
                m.Name,
                m.Email,
                m.Message,
                m.Timestamp
            })
            .ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(messages).ConfigureAwait(false);
        return response;
    }
}
