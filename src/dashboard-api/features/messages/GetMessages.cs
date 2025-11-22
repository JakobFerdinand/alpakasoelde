using System.Net;
using Azure.Data.Tables;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Messages;

public sealed class GetMessages
{
	public sealed class Function(Handler handler, ILogger<Function> logger)
	{
		private readonly Handler _handler = handler;
		private readonly ILogger<Function> _logger = logger;

		[Function("get-messages")]
		public async Task<HttpResponseData> Run(
			[HttpTrigger(AuthorizationLevel.Function, "get", Route = "messages")] HttpRequestData req)
		{
			var messages = await _handler.HandleAsync(new Query(), req.FunctionContext.CancellationToken);
			var response = req.CreateResponse(HttpStatusCode.OK);
			await response.WriteAsJsonAsync(messages).ConfigureAwait(false);
			return response;
		}
	}

	public sealed record Query;

	public sealed record DashboardMessage(string Id, string Name, string Email, string Message, DateTimeOffset? Timestamp);

	public interface IReadStore
	{
		Task<IReadOnlyList<MessageEntity>> GetAllAsync(CancellationToken cancellationToken);
	}

	public sealed class TableReadStore(TableServiceClient tableServiceClient) : IReadStore
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

	public sealed class Handler(IReadStore store)
	{
		private readonly IReadStore _store = store;

		public async Task<IReadOnlyList<DashboardMessage>> HandleAsync(Query query, CancellationToken cancellationToken)
		{
			IReadOnlyList<MessageEntity> messages = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);
			return messages
				.OrderByDescending(m => m.Timestamp ?? DateTimeOffset.MinValue)
				.Select(m => new DashboardMessage(m.RowKey, m.Name, m.Email, m.Message, m.Timestamp))
				.ToList();
		}
	}
}
