using System.Net;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Messages;

public sealed class GetOldMessageCount
{
	public sealed class Function(Handler handler, ILogger<Function> logger)
	{
		private readonly Handler _handler = handler;
		private readonly ILogger<Function> _logger = logger;

		[Function("get-old-message-count")]
		public async Task<HttpResponseData> Run(
			[HttpTrigger(AuthorizationLevel.Function, "get", Route = "messages/count-old")] HttpRequestData req)
		{
			Result result = await _handler.HandleAsync(new Query(TimeSpan.FromDays(30 * 6)), req.FunctionContext.CancellationToken);
			var response = req.CreateResponse(HttpStatusCode.OK);
			await response.WriteAsJsonAsync(result).ConfigureAwait(false);
			return response;
		}
	}

	public sealed record Query(TimeSpan AgeThreshold);

	public sealed record Result(int Count);

	public sealed class Handler(GetMessages.IReadStore store)
	{
		private readonly GetMessages.IReadStore _store = store;

		public async Task<Result> HandleAsync(Query query, CancellationToken cancellationToken)
		{
			IReadOnlyList<MessageEntity> messages = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);
			DateTimeOffset threshold = DateTimeOffset.UtcNow.Subtract(query.AgeThreshold);
			int count = messages.Count(m => m.Timestamp < threshold);
			return new Result(count);
		}
	}
}
