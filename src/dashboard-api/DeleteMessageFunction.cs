using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace DashboardApi;

public class DeleteMessageFunction(ILoggerFactory loggerFactory, TableServiceClient tableServiceClient)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<DeleteMessageFunction>();
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;

    [Function("delete-message")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "messages/{rowKey}")] HttpRequestData req,
        string rowKey)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("messages");

        try
        {
            await tableClient.DeleteEntityAsync("ContactPartition", rowKey).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Message with RowKey {RowKey} not found for deletion.", rowKey);
            var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
            await notFoundResponse.WriteAsJsonAsync(new
            {
                title = "Not Found",
                status = (int)HttpStatusCode.NotFound,
                detail = $"Message with id '{rowKey}' was not found."
            }).ConfigureAwait(false);

            return notFoundResponse;
        }

        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}
