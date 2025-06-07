using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Api;

public class GetOldMessageCountFunction(ILoggerFactory loggerFactory, TableServiceClient tableServiceClient)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GetOldMessageCountFunction>();
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;

    [Function("get-old-message-count")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dashboard/messages/count-old")] HttpRequestData req)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("messages");

        DateTimeOffset threshold = DateTimeOffset.UtcNow.AddMonths(-6);
        int count = tableClient.Query<MessageEntity>(m => m.Timestamp < threshold).Count();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { Count = count }).ConfigureAwait(false);
        return response;
    }
}
