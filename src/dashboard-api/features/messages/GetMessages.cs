using System.Net;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Shared.Persistence.Entities;

namespace DashboardApi.Features.Messages;

public sealed record GetMessagesQuery;

public sealed record DashboardMessage(string Id, string Name, string Email, string Message, DateTimeOffset? Timestamp);

public interface IMessageReadStore
{
    Task<IReadOnlyList<MessageEntity>> GetAllAsync(CancellationToken cancellationToken);
}

public sealed class TableMessageReadStore(TableServiceClient tableServiceClient) : IMessageReadStore
{
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;

    public Task<IReadOnlyList<MessageEntity>> GetAllAsync(CancellationToken cancellationToken)
    {
        TableClient tableClient = _tableServiceClient.GetTableClient("messages");
        var items = tableClient
            .Query<MessageEntity>()
            .OrderByDescending(m => m.Timestamp)
            .ToList();
        return Task.FromResult<IReadOnlyList<MessageEntity>>(items);
    }
}

public sealed class GetMessagesHandler(IMessageReadStore store)
{
    private readonly IMessageReadStore _store = store;

    public async Task<IReadOnlyList<DashboardMessage>> HandleAsync(GetMessagesQuery query, CancellationToken cancellationToken)
    {
        IReadOnlyList<MessageEntity> messages = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return messages
            .Select(m => new DashboardMessage(m.RowKey, m.Name, m.Email, m.Message, m.Timestamp))
            .ToList();
    }
}

public sealed class GetMessagesFunction(GetMessagesHandler handler, ILogger<GetMessagesFunction> logger)
{
    private readonly GetMessagesHandler _handler = handler;
    private readonly ILogger<GetMessagesFunction> _logger = logger;

    [Function("get-messages")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "messages")] HttpRequestData req)
    {
        var messages = await _handler.HandleAsync(new GetMessagesQuery(), req.FunctionContext.CancellationToken);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(messages).ConfigureAwait(false);
        return response;
    }
}
