using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Messages;

public sealed record DeleteMessageCommand(string RowKey);

public sealed class DeleteMessageHandler(IMessageDeleteStore store, ILogger<DeleteMessageHandler> logger)
{
    private readonly IMessageDeleteStore _store = store;
    private readonly ILogger<DeleteMessageHandler> _logger = logger;

    public async Task<bool> HandleAsync(DeleteMessageCommand command, CancellationToken cancellationToken)
    {
        try
        {
            await _store.DeleteAsync(command.RowKey, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == (int)HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Message with RowKey {RowKey} not found for deletion.", command.RowKey);
            return false;
        }
    }
}

public interface IMessageDeleteStore
{
    Task DeleteAsync(string rowKey, CancellationToken cancellationToken);
}

public sealed class TableMessageDeleteStore(TableServiceClient tableServiceClient) : IMessageDeleteStore
{
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;

    public Task DeleteAsync(string rowKey, CancellationToken cancellationToken)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("messages");
        return tableClient.DeleteEntityAsync("ContactPartition", rowKey, cancellationToken: cancellationToken);
    }
}

public sealed class DeleteMessageFunction(DeleteMessageHandler handler, ILogger<DeleteMessageFunction> logger)
{
    private readonly DeleteMessageHandler _handler = handler;
    private readonly ILogger<DeleteMessageFunction> _logger = logger;

    [Function("delete-message")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "messages/{rowKey}")] HttpRequestData req,
        string rowKey)
    {
        bool deleted = await _handler.HandleAsync(new DeleteMessageCommand(rowKey), req.FunctionContext.CancellationToken);
        if (!deleted)
        {
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
