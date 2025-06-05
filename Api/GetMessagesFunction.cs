using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Api;

public class GetMessagesFunction(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GetMessagesFunction>();

    [Function("get-messages")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req)
    {
        string? connectionString = Environment.GetEnvironmentVariable("MessageStorageConnection");
        TableClient tableClient = new(connectionString, "messages");

        var messages = tableClient.Query<MessageEntity>().ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(messages).ConfigureAwait(false);
        return response;
    }
}
