using System.Net;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.PageViews;

public sealed class GetPageViewSessions
{
	private const int TableLookbackDays = 180;
	private const int MaxSessionIdLength = 64;

	private readonly Handler _handler;
	private readonly ILogger<GetPageViewSessions> _logger;

	public GetPageViewSessions(Handler handler, ILogger<GetPageViewSessions> logger)
	{
		_handler = handler;
		_logger = logger;
	}

	[Function("get-pageview-sessions")]
	public async Task<HttpResponseData> RunSessions(
		[HttpTrigger(AuthorizationLevel.Function, "get", Route = "pageviews/sessions")] HttpRequestData req)
	{
		int days = 28;
		if (int.TryParse(req.Query["days"], out int requestedDays) && requestedDays > 0)
		{
			days = Math.Min(requestedDays, TableLookbackDays);
		}

		DateTimeOffset? windowStart = null;
		string? fromParam = req.Query["from"];
		string? toParam = req.Query["to"];
		if (DateOnly.TryParse(fromParam, out DateOnly from) && DateOnly.TryParse(toParam, out DateOnly to) && to >= from)
		{
			int computedDays = (to.DayNumber - from.DayNumber) + 1;
			days = Math.Min(computedDays, TableLookbackDays);
			windowStart = new DateTimeOffset(from.Year, from.Month, from.Day, 0, 0, 0, TimeSpan.Zero);
		}

		int minPages = 1;
		if (int.TryParse(req.Query["minPages"], out int requestedMinPages) && requestedMinPages > 0)
		{
			minPages = Math.Min(requestedMinPages, 100);
		}

		int limit = 50;
		if (int.TryParse(req.Query["limit"], out int requestedLimit) && requestedLimit > 0)
		{
			limit = Math.Min(requestedLimit, 200);
		}

		string? visitor = req.Query["visitor"]?.Trim();
		if (string.IsNullOrEmpty(visitor) || visitor.Length > MaxSessionIdLength)
		{
			visitor = null;
		}

		string? path = req.Query["path"]?.Trim();
		if (string.IsNullOrEmpty(path))
		{
			path = null;
		}

		SessionListResult result = await _handler.HandleListAsync(new ListQuery(days, minPages, limit, visitor, path, windowStart), req.FunctionContext.CancellationToken);
		var response = req.CreateResponse(HttpStatusCode.OK);
		await response.WriteAsJsonAsync(result).ConfigureAwait(false);
		return response;
	}

	[Function("get-pageview-session-by-id")]
	public async Task<HttpResponseData> RunSessionById(
		[HttpTrigger(AuthorizationLevel.Function, "get", Route = "pageviews/sessions/{sessionId}")] HttpRequestData req,
		string sessionId)
	{
		string trimmedId = sessionId.Trim();
		if (trimmedId.Length == 0 || trimmedId.Length > MaxSessionIdLength)
		{
			var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
			await badRequest.WriteStringAsync("Ungültige Sitzungs-ID.").ConfigureAwait(false);
			return badRequest;
		}

		SessionDetailResult? result = await _handler.HandleDetailAsync(trimmedId, req.FunctionContext.CancellationToken);
		if (result is null)
		{
			var notFound = req.CreateResponse(HttpStatusCode.NotFound);
			await notFound.WriteStringAsync("Sitzung nicht gefunden.").ConfigureAwait(false);
			return notFound;
		}

		var response = req.CreateResponse(HttpStatusCode.OK);
		await response.WriteAsJsonAsync(result).ConfigureAwait(false);
		return response;
	}

	public sealed record ListQuery(int Days, int MinPages, int Limit, string? Visitor, string? Path, DateTimeOffset? WindowStart = null);

	public sealed record SessionSummary(string SessionId, string? VisitorId, DateTimeOffset StartedAt, DateTimeOffset LastSeenAt, int PageViews, int DurationSeconds, string EntryPath, string ExitPath, string? EntryReferrerHost, string DeviceCategory);

	public sealed record SessionEvent(DateTimeOffset TimestampUtc, string Path, string? ReferrerHost, string? NavigationType, string DeviceCategory, double? DwellSeconds);

	public sealed record SessionListResult(int WindowDays, IReadOnlyList<SessionSummary> Sessions, bool Truncated, int UngroupedPageViews);

	public sealed record SessionDetailResult(SessionSummary Summary, IReadOnlyList<SessionEvent> Events);

	public sealed class Handler(GetPageViewStats.IPageViewReadStore store)
	{
		private readonly GetPageViewStats.IPageViewReadStore _store = store;

		public async Task<SessionListResult> HandleListAsync(ListQuery query, CancellationToken cancellationToken)
		{
			IReadOnlyList<PageViewEntity> pageViews = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);

			DateTimeOffset windowStart = query.WindowStart ?? DateTimeOffset.UtcNow.AddDays(-query.Days);

			var inWindow = pageViews
				.Where(p => p.Timestamp.HasValue && p.Timestamp >= windowStart)
				.ToList();

			int ungroupedPageViews = inWindow.Count(p => string.IsNullOrWhiteSpace(p.SessionId));

			IEnumerable<(SessionSummary Summary, List<PageViewEntity> Events)> sessions = inWindow
				.Where(p => !string.IsNullOrWhiteSpace(p.SessionId))
				.GroupBy(p => p.SessionId!.Trim(), StringComparer.Ordinal)
				.Select(g =>
				{
					List<PageViewEntity> events = OrderEvents(g.ToList());
					return (BuildSummary(events), events);
				});

			sessions = sessions.Where(s => s.Item1.PageViews >= query.MinPages);

			if (!string.IsNullOrWhiteSpace(query.Visitor))
			{
				sessions = sessions.Where(s => s.Item1.VisitorId != null && string.Equals(s.Item1.VisitorId, query.Visitor, StringComparison.Ordinal));
			}

			if (!string.IsNullOrWhiteSpace(query.Path))
			{
				sessions = sessions.Where(s => s.Events.Any(e => string.Equals(NormalizePath(e.Path), query.Path, StringComparison.Ordinal)));
			}

			List<SessionSummary> matching = sessions.Select(s => s.Item1).ToList();

			bool truncated = matching.Count > query.Limit;

			List<SessionSummary> result = matching
				.OrderByDescending(s => s.LastSeenAt)
				.Take(query.Limit)
				.ToList();

			return new SessionListResult(query.Days, result, truncated, ungroupedPageViews);
		}

		public async Task<SessionDetailResult?> HandleDetailAsync(string sessionId, CancellationToken cancellationToken)
		{
			IReadOnlyList<PageViewEntity> pageViews = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);

			List<PageViewEntity> unfilteredMatches = pageViews
				.Where(p => p.SessionId != null && string.Equals(p.SessionId.Trim(), sessionId, StringComparison.Ordinal))
				.ToList();

			if (unfilteredMatches.Count == 0)
			{
				return null;
			}

			List<PageViewEntity> events = OrderEvents(unfilteredMatches);

			var result = new List<SessionEvent>(events.Count);
			for (int i = 0; i < events.Count; i++)
			{
				PageViewEntity current = events[i];
				double? dwellSeconds = i < events.Count - 1
					? Math.Round((events[i + 1].Timestamp!.Value - current.Timestamp!.Value).TotalSeconds, 1)
					: null;
				result.Add(new SessionEvent(
					current.Timestamp!.Value,
					current.Path,
					current.ReferrerHost,
					current.NavigationType,
					GetDeviceCategory(current.ViewportWidth),
					dwellSeconds));
			}

			return new SessionDetailResult(BuildSummary(events), result);
		}

		private static List<PageViewEntity> OrderEvents(List<PageViewEntity> events)
		{
			return events
				.OrderBy(e => e.Timestamp)
				.ThenBy(e => e.RowKey, StringComparer.Ordinal)
				.ToList();
		}

		private static SessionSummary BuildSummary(List<PageViewEntity> events)
		{
			PageViewEntity first = events[0];
			PageViewEntity last = events[^1];
			DateTimeOffset startedAt = first.Timestamp!.Value;
			DateTimeOffset lastSeenAt = last.Timestamp!.Value;
			return new SessionSummary(
				first.SessionId!.Trim(),
				string.IsNullOrWhiteSpace(first.VisitorId) ? null : first.VisitorId!.Trim(),
				startedAt,
				lastSeenAt,
				events.Count,
				(int)(lastSeenAt - startedAt).TotalSeconds,
				NormalizePath(first.Path),
				NormalizePath(last.Path),
				first.ReferrerHost,
				GetDeviceCategory(first.ViewportWidth));
		}

		private static string NormalizePath(string path)
		{
			string trimmed = path.Trim().TrimEnd('/');
			return trimmed.Length == 0 ? "/" : trimmed;
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
	}
}
