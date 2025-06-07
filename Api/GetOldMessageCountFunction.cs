using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Api;

public class GetOldMessageCountFunction(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GetOldMessageCountFunction>();

    [Function("get-old-message-count")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dashboard/get-old-message-count")] HttpRequestData req)
    {
        string? connectionString = Environment.GetEnvironmentVariable("MessageStorageConnection");
        TableClient tableClient = new(connectionString, "messages");

        DateTimeOffset threshold = DateTimeOffset.UtcNow.AddMonths(-6);
        int count = tableClient.Query<MessageEntity>(m => m.Timestamp < threshold).Count();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { Count = count }).ConfigureAwait(false);
        return response;
    }
}
