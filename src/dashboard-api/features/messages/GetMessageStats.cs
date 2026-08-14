using System.Net;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Messages;

public sealed class GetMessageStats
{
	private readonly Handler _handler;
	private readonly ILogger<GetMessageStats> _logger;

	public GetMessageStats(Handler handler, ILogger<GetMessageStats> logger)
	{
		_handler = handler;
		_logger = logger;
	}

	[Function("get-message-stats")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Function, "get", Route = "messages/stats")] HttpRequestData req)
	{
		int days = 28;
		if (int.TryParse(req.Query["days"], out int requestedDays) && requestedDays > 0)
		{
			days = requestedDays;
		}

		Result result = await _handler.HandleAsync(new Query(days), req.FunctionContext.CancellationToken);
		var response = req.CreateResponse(HttpStatusCode.OK);
		await response.WriteAsJsonAsync(result).ConfigureAwait(false);
		return response;
	}

	public sealed record Query(int Days);

	public sealed record Result(int Total, int Spam, int Legit, int OldCount, IReadOnlyList<PeriodBucket> Series);

	public sealed record PeriodBucket(string Period, int Spam, int Legit);

	public sealed class Handler(GetMessages.IReadStore store)
	{
		private const int OldThresholdDays = 30 * 6;
		private readonly GetMessages.IReadStore _store = store;

		public async Task<Result> HandleAsync(Query query, CancellationToken cancellationToken)
		{
			IReadOnlyList<MessageEntity> messages = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);

			DateTimeOffset now = DateTimeOffset.UtcNow;
			DateTimeOffset windowStart = now.AddDays(-query.Days);
			DateTimeOffset oldThreshold = now.AddDays(-OldThresholdDays);

			var inWindow = messages
				.Where(m => m.Timestamp.HasValue && m.Timestamp >= windowStart)
				.ToList();

			int total = inWindow.Count;
			int spam = inWindow.Count(m => m.IsSpam);
			int oldCount = messages.Count(m => m.Timestamp < oldThreshold);

			var buckets = new Dictionary<DateTime, (int Spam, int Legit)>();
			foreach (MessageEntity message in inWindow)
			{
				DateTime weekStart = GetWeekStart(message.Timestamp!.Value);
				(int Spam, int Legit) current = buckets.GetValueOrDefault(weekStart);
				if (message.IsSpam)
				{
					current.Spam++;
				}
				else
				{
					current.Legit++;
				}
				buckets[weekStart] = current;
			}

			List<PeriodBucket> series = [];
			for (DateTime week = GetWeekStart(windowStart); week <= GetWeekStart(now); week = week.AddDays(7))
			{
				(int Spam, int Legit) counts = buckets.GetValueOrDefault(week);
				series.Add(new PeriodBucket(week.ToString("yyyy-MM-dd"), counts.Spam, counts.Legit));
			}

			return new Result(total, spam, total - spam, oldCount, series);
		}

		private static DateTime GetWeekStart(DateTimeOffset value)
		{
			DateTime date = value.UtcDateTime.Date;
			int diff = ((int)date.DayOfWeek + 6) % 7;
			return date.AddDays(-diff);
		}
	}
}