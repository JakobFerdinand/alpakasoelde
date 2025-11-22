using System.Net;
using Azure.Data.Tables;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Messages;

public sealed record GetOldMessageCountQuery(TimeSpan AgeThreshold);

public sealed record OldMessageCountResult(int Count);

public sealed class GetOldMessageCountHandler(IMessageReadStore store)
{
	private readonly IMessageReadStore _store = store;

	public async Task<OldMessageCountResult> HandleAsync(GetOldMessageCountQuery query, CancellationToken cancellationToken)
	{
		IReadOnlyList<MessageEntity> messages = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);
		DateTimeOffset threshold = DateTimeOffset.UtcNow.Subtract(query.AgeThreshold);
		int count = messages.Count(m => m.Timestamp < threshold);
		return new OldMessageCountResult(count);
	}
}

public sealed class GetOldMessageCountFunction(GetOldMessageCountHandler handler, ILogger<GetOldMessageCountFunction> logger)
{
	private readonly GetOldMessageCountHandler _handler = handler;
	private readonly ILogger<GetOldMessageCountFunction> _logger = logger;

	[Function("get-old-message-count")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Function, "get", Route = "messages/count-old")] HttpRequestData req)
	{
		OldMessageCountResult result = await _handler.HandleAsync(new GetOldMessageCountQuery(TimeSpan.FromDays(30 * 6)), req.FunctionContext.CancellationToken);
		var response = req.CreateResponse(HttpStatusCode.OK);
		await response.WriteAsJsonAsync(result).ConfigureAwait(false);
		return response;
	}
}
