using System.Globalization;
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

		string? week = req.Query["week"];
		if (!string.IsNullOrWhiteSpace(week) && !DateTime.TryParseExact(week, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
		{
			week = null;
		}

		Result result = await _handler.HandleAsync(new Query(days, week), req.FunctionContext.CancellationToken);
		var response = req.CreateResponse(HttpStatusCode.OK);
		await response.WriteAsJsonAsync(result).ConfigureAwait(false);
		return response;
	}

	public sealed record Query(int Days, string? Week);

	public sealed record Result(int Total, int UniquePaths, IReadOnlyList<PathCount> TopPaths, IReadOnlyList<PeriodBucket> Series, IReadOnlyList<PathPeriodBucket> PathSeries, IReadOnlyList<DeviceCount> Devices, IReadOnlyList<DevicePeriodBucket> DeviceSeries, IReadOnlyList<OriginCount> Origins, IReadOnlyList<OriginPeriodBucket> OriginSeries);

	public sealed record PathCount(string Path, int Count);

	public sealed record DeviceCount(string Category, int Count);

	public sealed record OriginCount(string Domain, int Count);

	public sealed record PeriodBucket(string Period, int Count);

	public sealed record PathPeriodBucket(string Period, string Path, int Count);

	public sealed record DevicePeriodBucket(string Period, string Category, int Count);

	public sealed record OriginPeriodBucket(string Period, string Domain, int Count);

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
		private const int ChartPathsLimit = 6;
		private const int ChartOriginsLimit = 6;
		private const string OtherBucketLabel = "Übrige";
		private static readonly string[] DeviceCategories = ["Mobil", "Tablet", "Laptop", "Breitbild"];

		private readonly IPageViewReadStore _store = store;

		public async Task<Result> HandleAsync(Query query, CancellationToken cancellationToken)
		{
			IReadOnlyList<PageViewEntity> pageViews = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);

			DateTimeOffset now = DateTimeOffset.UtcNow;
			DateTime? weekStart = query.Week is null
				? null
				: DateTime.ParseExact(query.Week, "yyyy-MM-dd", CultureInfo.InvariantCulture);
			DateTimeOffset windowStart = now.AddDays(-query.Days);

			var inWindow = pageViews
				.Where(p => p.Timestamp.HasValue && IsInRange(p.Timestamp.Value, weekStart, now, query.Days))
				.ToList();

			int total = inWindow.Count;
			int uniquePaths = inWindow.Select(p => p.Path).Distinct().Count();

			List<PathCount> topPaths = inWindow
				.GroupBy(p => p.Path)
				.OrderByDescending(g => g.Count())
				.ThenBy(g => g.Key)
				.Take(10)
				.Select(g => new PathCount(g.Key, g.Count()))
				.ToList();

			List<DeviceCount> devices = inWindow
				.GroupBy(p => GetDeviceCategory(p.ViewportWidth))
				.OrderByDescending(g => g.Count())
				.ThenBy(g => Array.IndexOf(DeviceCategories, g.Key))
				.Select(g => new DeviceCount(g.Key, g.Count()))
				.ToList();

			var externalPageViews = inWindow
				.Where(p => !string.IsNullOrWhiteSpace(p.ReferrerHost) && !IsInternalReferrer(p.ReferrerHost))
				.ToList();

			List<OriginCount> origins = externalPageViews
				.GroupBy(p => p.ReferrerHost!.Trim(), StringComparer.OrdinalIgnoreCase)
				.OrderByDescending(g => g.Count())
				.ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
				.Take(10)
				.Select(g => new OriginCount(g.Key.ToLowerInvariant(), g.Count()))
				.ToList();

			List<string> chartPaths = topPaths.Take(ChartPathsLimit).Select(p => p.Path).ToList();
			int chartTopCount = inWindow.Count(p => chartPaths.Contains(p.Path));
			if (total - chartTopCount > 0)
			{
				chartPaths.Add(OtherBucketLabel);
			}

			HashSet<string> chartPathSet = [.. chartPaths];
			List<string> chartOrigins = origins.Take(ChartOriginsLimit).Select(o => o.Domain).ToList();
			if (externalPageViews.Count - origins.Take(ChartOriginsLimit).Sum(o => o.Count) > 0)
			{
				chartOrigins.Add(OtherBucketLabel);
			}
			HashSet<string> chartOriginSet = new(chartOrigins, StringComparer.OrdinalIgnoreCase);

			var buckets = new Dictionary<DateTime, int>();
			var pathBuckets = new Dictionary<(DateTime Bucket, string Path), int>();
			var deviceBuckets = new Dictionary<(DateTime Bucket, string Category), int>();
			var originBuckets = new Dictionary<(DateTime Bucket, string Domain), int>();
			foreach (PageViewEntity pageView in inWindow)
			{
				DateTime bucketStart = GetBucketStart(pageView.Timestamp!.Value, weekStart);
				buckets[bucketStart] = buckets.GetValueOrDefault(bucketStart) + 1;

				string path = chartPathSet.Contains(pageView.Path) ? pageView.Path : OtherBucketLabel;
				var pathKey = (bucketStart, path);
				pathBuckets[pathKey] = pathBuckets.GetValueOrDefault(pathKey) + 1;

				string category = GetDeviceCategory(pageView.ViewportWidth);
				var deviceKey = (bucketStart, category);
				deviceBuckets[deviceKey] = deviceBuckets.GetValueOrDefault(deviceKey) + 1;

				if (!string.IsNullOrWhiteSpace(pageView.ReferrerHost) && !IsInternalReferrer(pageView.ReferrerHost))
				{
					string domain = pageView.ReferrerHost.Trim().ToLowerInvariant();
					domain = chartOriginSet.Contains(domain) ? domain : OtherBucketLabel;
					var originKey = (bucketStart, domain);
					originBuckets[originKey] = originBuckets.GetValueOrDefault(originKey) + 1;
				}
			}

			List<PeriodBucket> series = [];
			List<PathPeriodBucket> pathSeries = [];
			List<DevicePeriodBucket> deviceSeries = [];
			List<OriginPeriodBucket> originSeries = [];
			foreach (DateTime bucketStart in GetBucketStarts(weekStart, windowStart, now))
			{
				series.Add(new PeriodBucket(bucketStart.ToString("yyyy-MM-dd"), buckets.GetValueOrDefault(bucketStart)));

				foreach (string path in chartPaths)
				{
					pathSeries.Add(new PathPeriodBucket(bucketStart.ToString("yyyy-MM-dd"), path, pathBuckets.GetValueOrDefault((bucketStart, path))));
				}

				foreach (string category in DeviceCategories)
				{
					deviceSeries.Add(new DevicePeriodBucket(bucketStart.ToString("yyyy-MM-dd"), category, deviceBuckets.GetValueOrDefault((bucketStart, category))));
				}

				foreach (string domain in chartOrigins)
				{
					originSeries.Add(new OriginPeriodBucket(bucketStart.ToString("yyyy-MM-dd"), domain, originBuckets.GetValueOrDefault((bucketStart, domain))));
				}
			}

			return new Result(total, uniquePaths, topPaths, series, pathSeries, devices, deviceSeries, origins, originSeries);
		}

		private static bool IsInRange(DateTimeOffset timestamp, DateTime? weekStart, DateTimeOffset now, int days)
		{
			if (weekStart.HasValue)
			{
				return timestamp >= weekStart.Value && timestamp < weekStart.Value.AddDays(7);
			}

			return timestamp >= now.AddDays(-days);
		}

		private static IEnumerable<DateTime> GetBucketStarts(DateTime? weekStart, DateTimeOffset windowStart, DateTimeOffset now)
		{
			if (weekStart.HasValue)
			{
				for (int offset = 0; offset < 7; offset++)
				{
					yield return weekStart.Value.AddDays(offset);
				}

				yield break;
			}

			for (DateTime week = GetWeekStart(windowStart); week <= GetWeekStart(now); week = week.AddDays(7))
			{
				yield return week;
			}
		}

		private static DateTime GetBucketStart(DateTimeOffset value, DateTime? weekStart)
		{
			return weekStart.HasValue ? value.UtcDateTime.Date : GetWeekStart(value);
		}

		private static bool IsInternalReferrer(string referrerHost)
		{
			string host = referrerHost.Trim();
			return host.Equals("alpakasoelde.at", StringComparison.OrdinalIgnoreCase)
				|| (host.StartsWith("lemon-hill-0ebd24003-", StringComparison.OrdinalIgnoreCase)
					&& host.EndsWith(".westeurope.6.azurestaticapps.net", StringComparison.OrdinalIgnoreCase));
		}

		private static string GetDeviceCategory(int viewportWidth)
		{
			return viewportWidth switch
			{
				< 600 => "Mobil",
				< 1024 => "Tablet",
				< 1920 => "Laptop",
				_ => "Breitbild",
			};
		}

		private static DateTime GetWeekStart(DateTimeOffset value)
		{
			DateTime date = value.UtcDateTime.Date;
			int diff = ((int)date.DayOfWeek + 6) % 7;
			return date.AddDays(-diff);
		}
	}
}
