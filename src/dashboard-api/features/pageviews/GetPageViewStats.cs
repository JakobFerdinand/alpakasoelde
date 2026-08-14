using System.Net;
using Azure.Data.Tables;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.PageViews;

public sealed class GetPageViewStats
{
	private const int TableLookbackDays = 180;

	private readonly Handler _handler;
	private readonly ILogger<GetPageViewStats> _logger;

	public GetPageViewStats(Handler handler, ILogger<GetPageViewStats> logger)
	{
		_handler = handler;
		_logger = logger;
	}

	[Function("get-pageview-stats")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Function, "get", Route = "pageviews/stats")] HttpRequestData req)
	{
		int days = 28;
		if (int.TryParse(req.Query["days"], out int requestedDays) && requestedDays > 0)
		{
			days = Math.Min(requestedDays, TableLookbackDays);
		}

		Result result = await _handler.HandleAsync(new Query(days), req.FunctionContext.CancellationToken);
		var response = req.CreateResponse(HttpStatusCode.OK);
		await response.WriteAsJsonAsync(result).ConfigureAwait(false);
		return response;
	}

	public sealed record Query(int Days);

	public sealed record Result(int Total, IReadOnlyList<PathCount> TopPaths, IReadOnlyList<PeriodBucket> Series);

	public sealed record PathCount(string Path, int Count);

	public sealed record PeriodBucket(string Period, int Count);

	public interface IPageViewReadStore
	{
		Task<IReadOnlyList<PageViewEntity>> GetAllAsync(CancellationToken cancellationToken);
	}

	public sealed class TablePageViewReadStore(TableServiceClient tableServiceClient) : IPageViewReadStore
	{
		private readonly TableServiceClient _tableServiceClient = tableServiceClient;

		public async Task<IReadOnlyList<PageViewEntity>> GetAllAsync(CancellationToken cancellationToken)
		{
			TableClient tableClient = _tableServiceClient.GetTableClient("pageviews");

			string windowStartDate = DateTime.UtcNow.AddDays(-TableLookbackDays).ToString("yyyy-MM-dd");
			string todayDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
			string filter = $"PartitionKey ge 'Pv|{windowStartDate}' and PartitionKey le 'Pv|{todayDate}'";

			var items = new List<PageViewEntity>();
			await foreach (PageViewEntity entity in tableClient.QueryAsync<PageViewEntity>(filter, cancellationToken: cancellationToken))
			{
				items.Add(entity);
			}

			return items;
		}
	}

	public sealed class Handler(IPageViewReadStore store)
	{
		private readonly IPageViewReadStore _store = store;

		public async Task<Result> HandleAsync(Query query, CancellationToken cancellationToken)
		{
			IReadOnlyList<PageViewEntity> pageViews = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);

			DateTimeOffset now = DateTimeOffset.UtcNow;
			DateTimeOffset windowStart = now.AddDays(-query.Days);

			var inWindow = pageViews
				.Where(p => p.Timestamp.HasValue && p.Timestamp >= windowStart)
				.ToList();

			int total = inWindow.Count;

			List<PathCount> topPaths = inWindow
				.GroupBy(p => p.Path)
				.OrderByDescending(g => g.Count())
				.ThenBy(g => g.Key)
				.Take(10)
				.Select(g => new PathCount(g.Key, g.Count()))
				.ToList();

			var buckets = new Dictionary<DateTime, int>();
			foreach (PageViewEntity pageView in inWindow)
			{
				DateTime weekStart = GetWeekStart(pageView.Timestamp!.Value);
				buckets[weekStart] = buckets.GetValueOrDefault(weekStart) + 1;
			}

			List<PeriodBucket> series = [];
			for (DateTime week = GetWeekStart(windowStart); week <= GetWeekStart(now); week = week.AddDays(7))
			{
				series.Add(new PeriodBucket(week.ToString("yyyy-MM-dd"), buckets.GetValueOrDefault(week)));
			}

			return new Result(total, topPaths, series);
		}

		private static DateTime GetWeekStart(DateTimeOffset value)
		{
			DateTime date = value.UtcDateTime.Date;
			int diff = ((int)date.DayOfWeek + 6) % 7;
			return date.AddDays(-diff);
		}
	}
}