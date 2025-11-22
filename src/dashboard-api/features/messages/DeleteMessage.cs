using System.Net;
using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Messages;

public sealed class DeleteMessage
{
    private readonly Handler _handler;
    private readonly ILogger<DeleteMessage> _logger;

    public DeleteMessage(Handler handler, ILogger<DeleteMessage> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [Function("delete-message")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "messages/{rowKey}")] HttpRequestData req,
        string rowKey)
    {
        bool deleted = await _handler.HandleAsync(new Command(rowKey), req.FunctionContext.CancellationToken);
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

    public sealed record Command(string RowKey);

    public interface IStore
    {
        Task DeleteAsync(string rowKey, CancellationToken cancellationToken);
    }

    public sealed class TableStore(TableServiceClient tableServiceClient) : IStore
    {
        private readonly TableServiceClient _tableServiceClient = tableServiceClient;

        public Task DeleteAsync(string rowKey, CancellationToken cancellationToken)
        {
            TableClient tableClient = _tableServiceClient.GetTableClient("messages");
            return tableClient.DeleteEntityAsync("ContactPartition", rowKey, cancellationToken: cancellationToken);
        }
    }

    public sealed class Handler(IStore store, ILogger<Handler> logger)
    {
        private readonly IStore _store = store;
        private readonly ILogger<Handler> _logger = logger;

        public async Task<bool> HandleAsync(Command command, CancellationToken cancellationToken)
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
}
