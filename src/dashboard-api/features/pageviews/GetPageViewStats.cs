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

		string? granularityParam = req.Query["granularity"];
		string granularity = granularityParam is "week" or "day" or "hour" ? granularityParam : "week";

		string? groupByParam = req.Query["groupBy"];
		string groupBy = groupByParam is "total" or "path" or "device" or "origin" ? groupByParam : "path";

		if (granularity == "hour")
		{
			days = Math.Min(days, 28);
		}

		Result result = await _handler.HandleAsync(new Query(days, granularity, groupBy), req.FunctionContext.CancellationToken);
		var response = req.CreateResponse(HttpStatusCode.OK);
		await response.WriteAsJsonAsync(result).ConfigureAwait(false);
		return response;
	}

	public sealed record Query(int Days, string Granularity, string GroupBy);

	public sealed record Result(int Total, int UniquePaths, IReadOnlyList<PathCount> TopPaths, IReadOnlyList<DeviceCount> Devices, IReadOnlyList<OriginCount> Origins, IReadOnlyList<Bucket> Series, int Sessions, int Visitors, IReadOnlyList<NavigationCount> Navigations, IReadOnlyList<AudienceBucket> AudienceSeries, string Granularity, string GroupBy);

	public sealed record PathCount(string Path, int Count);

	public sealed record DeviceCount(string Category, int Count);

	public sealed record OriginCount(string Domain, int Count);

	public sealed record NavigationCount(string Type, int Count);

	public sealed record Bucket(string Period, string? Group, int Count);

	public sealed record AudienceBucket(string Period, int Visitors, int Sessions);

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
			DateTimeOffset windowStart = now.AddDays(-query.Days);

			var inWindow = pageViews
				.Where(p => p.Timestamp.HasValue && p.Timestamp >= windowStart)
				.ToList();

			int total = inWindow.Count;
			int uniquePaths = inWindow.Select(p => NormalizePath(p.Path)).Distinct(StringComparer.Ordinal).Count();

			List<PathCount> topPaths = inWindow
				.GroupBy(p => NormalizePath(p.Path))
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

			int sessions = inWindow
				.Where(p => !string.IsNullOrWhiteSpace(p.SessionId))
				.Select(p => p.SessionId!.Trim())
				.Distinct(StringComparer.Ordinal)
				.Count();

			int visitors = inWindow
				.Where(p => !string.IsNullOrWhiteSpace(p.VisitorId))
				.Select(p => p.VisitorId!.Trim())
				.Distinct(StringComparer.Ordinal)
				.Count();

			List<NavigationCount> navigations = inWindow
				.Where(p => !string.IsNullOrWhiteSpace(p.NavigationType))
				.GroupBy(p => p.NavigationType!.Trim(), StringComparer.Ordinal)
				.OrderByDescending(g => g.Count())
				.ThenBy(g => g.Key, StringComparer.Ordinal)
				.Select(g => new NavigationCount(g.Key, g.Count()))
				.ToList();

			List<string> chartPaths = topPaths.Take(ChartPathsLimit).Select(p => p.Path).ToList();
			int chartTopCount = inWindow.Count(p => chartPaths.Contains(NormalizePath(p.Path)));
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

			var buckets = new Dictionary<(DateTime Period, string? Group), int>();
			foreach (PageViewEntity pageView in inWindow)
			{
				DateTime period = GetPeriodStart(pageView.Timestamp!.Value, query.Granularity);

				string? group;
				if (query.GroupBy == "total")
				{
					group = null;
				}
				else if (query.GroupBy == "path")
				{
					string normalizedPath = NormalizePath(pageView.Path);
					group = chartPathSet.Contains(normalizedPath) ? normalizedPath : OtherBucketLabel;
				}
				else if (query.GroupBy == "device")
				{
					group = GetDeviceCategory(pageView.ViewportWidth);
				}
				else
				{
					if (string.IsNullOrWhiteSpace(pageView.ReferrerHost) || IsInternalReferrer(pageView.ReferrerHost))
					{
						continue;
					}

					string domain = pageView.ReferrerHost.Trim().ToLowerInvariant();
					group = chartOriginSet.Contains(domain) ? domain : OtherBucketLabel;
				}

				var key = (period, group);
				buckets[key] = buckets.GetValueOrDefault(key) + 1;
			}

			List<Bucket> series = [];
			DateTime periodStart = GetPeriodStart(windowStart, query.Granularity);
			DateTime lastPeriodStart = GetPeriodStart(now, query.Granularity);
			TimeSpan step = query.Granularity switch
			{
				"hour" => TimeSpan.FromHours(1),
				"day" => TimeSpan.FromDays(1),
				_ => TimeSpan.FromDays(7),
			};

			IReadOnlyList<string> groups = query.GroupBy switch
			{
				"device" => DeviceCategories,
				"origin" => chartOrigins,
				_ => chartPaths,
			};

			for (DateTime period = periodStart; period <= lastPeriodStart; period += step)
			{
				string periodString = FormatPeriod(period, query.Granularity);

				if (query.GroupBy == "total")
				{
					series.Add(new Bucket(periodString, null, buckets.GetValueOrDefault((period, null))));
				}
				else
				{
					foreach (string group in groups)
					{
						series.Add(new Bucket(periodString, group, buckets.GetValueOrDefault((period, group))));
					}
				}
			}

			List<AudienceBucket> audienceSeries = BuildAudienceSeries(inWindow, windowStart, now, query.Granularity);

			return new Result(total, uniquePaths, topPaths, devices, origins, series, sessions, visitors, navigations, audienceSeries, query.Granularity, query.GroupBy);
		}

		private static string NormalizePath(string path)
		{
			string trimmed = path.Trim().TrimEnd('/');
			return trimmed.Length == 0 ? "/" : trimmed;
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

		private static DateTime GetPeriodStart(DateTimeOffset value, string granularity)
		{
			return granularity switch
			{
				"day" => value.UtcDateTime.Date,
				"hour" => value.UtcDateTime.Date.AddHours(value.UtcDateTime.Hour),
				_ => GetWeekStart(value),
			};
		}

		private static string FormatPeriod(DateTime period, string granularity)
		{
			return granularity == "hour" ? period.ToString("yyyy-MM-dd'T'HH:mm") : period.ToString("yyyy-MM-dd");
		}

		private static List<AudienceBucket> BuildAudienceSeries(
			IReadOnlyList<PageViewEntity> inWindow,
			DateTimeOffset windowStart,
			DateTimeOffset now,
			string granularity)
		{
			var byPeriod = new Dictionary<DateTime, (HashSet<string> Visitors, HashSet<string> Sessions)>();
			foreach (PageViewEntity pv in inWindow)
			{
				DateTime period = GetPeriodStart(pv.Timestamp!.Value, granularity);
				if (!byPeriod.TryGetValue(period, out var bucket))
				{
					bucket = (new HashSet<string>(StringComparer.Ordinal), new HashSet<string>(StringComparer.Ordinal));
					byPeriod[period] = bucket;
				}
				if (!string.IsNullOrWhiteSpace(pv.VisitorId))
				{
					bucket.Visitors.Add(pv.VisitorId!.Trim());
				}
				if (!string.IsNullOrWhiteSpace(pv.SessionId))
				{
					bucket.Sessions.Add(pv.SessionId!.Trim());
				}
			}

			var result = new List<AudienceBucket>();
			DateTime periodStart = GetPeriodStart(windowStart, granularity);
			DateTime lastPeriodStart = GetPeriodStart(now, granularity);
			TimeSpan step = granularity switch
			{
				"hour" => TimeSpan.FromHours(1),
				"day" => TimeSpan.FromDays(1),
				_ => TimeSpan.FromDays(7),
			};

			for (DateTime period = periodStart; period <= lastPeriodStart; period += step)
			{
				string periodString = FormatPeriod(period, granularity);
				if (byPeriod.TryGetValue(period, out var counts))
				{
					result.Add(new AudienceBucket(periodString, counts.Visitors.Count, counts.Sessions.Count));
				}
				else
				{
					result.Add(new AudienceBucket(periodString, 0, 0));
				}
			}

			return result;
		}
	}
}
