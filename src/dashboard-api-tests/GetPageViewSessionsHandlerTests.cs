using DashboardApi.Features.PageViews;
using DashboardApi.Tests.Fakes;
using dashboard_api.shared.entities;

namespace DashboardApi.Tests;

public sealed class GetPageViewSessionsHandlerTests
{
	private static readonly DateTimeOffset WindowStart = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
	private static DateTimeOffset At(int hour, int minute, int second = 0) => WindowStart.AddHours(hour).AddMinutes(minute).AddSeconds(second);

	private static InMemoryPageViewReadStore Store() => new(
		// One three-page session, deliberately seeded out of order.
		InMemoryPageViewReadStore.View(At(10, 2), "/kontakt", "s1-c", sessionId: "s1", visitorId: "v1"),
		InMemoryPageViewReadStore.View(At(10, 0), "/", "s1-a", sessionId: "s1", visitorId: "v1", referrerHost: "google.at", navigationType: "navigate"),
		InMemoryPageViewReadStore.View(At(10, 0, 30), "/alpakas/", "s1-b", sessionId: "s1", visitorId: "v1"),
		// A single-page mobile session, later than the first.
		InMemoryPageViewReadStore.View(At(11, 0), "/produkte", "s2-a", sessionId: "s2", visitorId: "v2", viewportWidth: 500),
		// A view the beacon could not attribute to a session.
		InMemoryPageViewReadStore.View(At(12, 0), "/", "orphan", sessionId: "   "),
		// Outside the window entirely.
		InMemoryPageViewReadStore.View(WindowStart.AddDays(-3), "/", "old", sessionId: "s0", visitorId: "v0"));

	private static GetPageViewSessions.ListQuery Query(int minPages = 1, int limit = 50, string? visitor = null, string? path = null)
		=> new(7, minPages, limit, visitor, path, WindowStart);

	[Fact]
	public async Task Sessions_are_summarised_newest_first_and_unattributed_views_are_counted_separately()
	{
		GetPageViewSessions.Handler handler = new(Store());

		GetPageViewSessions.SessionListResult result = await handler.HandleListAsync(Query(), TestContext.Current.CancellationToken);

		Assert.Equal(7, result.WindowDays);
		Assert.False(result.Truncated);
		Assert.Equal(1, result.UngroupedPageViews);
		Assert.Equal(["s2", "s1"], result.Sessions.Select(s => s.SessionId));

		GetPageViewSessions.SessionSummary s1 = result.Sessions[1];
		Assert.Equal("v1", s1.VisitorId);
		Assert.Equal(3, s1.PageViews);
		Assert.Equal(120, s1.DurationSeconds);
		Assert.Equal("/", s1.EntryPath);
		Assert.Equal("/kontakt", s1.ExitPath);
		Assert.Equal("google.at", s1.EntryReferrerHost);
		Assert.Equal("Laptop", s1.DeviceCategory);

		GetPageViewSessions.SessionSummary s2 = result.Sessions[0];
		Assert.Equal(1, s2.PageViews);
		Assert.Equal(0, s2.DurationSeconds);
		Assert.Equal("Mobil", s2.DeviceCategory);
	}

	[Fact]
	public async Task The_minimum_page_count_drops_single_page_sessions()
	{
		GetPageViewSessions.Handler handler = new(Store());

		GetPageViewSessions.SessionListResult result = await handler.HandleListAsync(Query(minPages: 2), TestContext.Current.CancellationToken);

		Assert.Equal(["s1"], result.Sessions.Select(s => s.SessionId));
	}

	[Fact]
	public async Task Filtering_by_visitor_and_by_path_narrows_the_list()
	{
		GetPageViewSessions.Handler handler = new(Store());

		GetPageViewSessions.SessionListResult byVisitor = await handler.HandleListAsync(Query(visitor: "v2"), TestContext.Current.CancellationToken);
		// The stored path is "/alpakas/" — the filter matches the normalised form.
		GetPageViewSessions.SessionListResult byPath = await handler.HandleListAsync(Query(path: "/alpakas"), TestContext.Current.CancellationToken);

		Assert.Equal(["s2"], byVisitor.Sessions.Select(s => s.SessionId));
		Assert.Equal(["s1"], byPath.Sessions.Select(s => s.SessionId));
	}

	[Fact]
	public async Task Hitting_the_limit_keeps_the_newest_sessions_and_flags_the_result_as_truncated()
	{
		GetPageViewSessions.Handler handler = new(Store());

		GetPageViewSessions.SessionListResult result = await handler.HandleListAsync(Query(limit: 1), TestContext.Current.CancellationToken);

		Assert.True(result.Truncated);
		Assert.Equal(["s2"], result.Sessions.Select(s => s.SessionId));
	}

	[Fact]
	public async Task The_session_detail_orders_the_events_and_measures_dwell_between_them()
	{
		GetPageViewSessions.Handler handler = new(Store());

		GetPageViewSessions.SessionDetailResult? detail = await handler.HandleDetailAsync("s1", TestContext.Current.CancellationToken);

		Assert.NotNull(detail);
		Assert.Equal(["/", "/alpakas/", "/kontakt"], detail.Events.Select(e => e.Path));
		Assert.Equal([30d, 90d, null], detail.Events.Select(e => e.DwellSeconds));
		Assert.Equal(3, detail.Summary.PageViews);
	}

	[Fact]
	public async Task An_unknown_session_has_no_detail()
	{
		GetPageViewSessions.Handler handler = new(Store());

		Assert.Null(await handler.HandleDetailAsync("gibt-es-nicht", TestContext.Current.CancellationToken));
	}

	[Theory]
	[InlineData(599, "Mobil")]
	[InlineData(600, "Tablet")]
	[InlineData(1023, "Tablet")]
	[InlineData(1024, "Laptop")]
	[InlineData(1919, "Laptop")]
	[InlineData(1920, "Breitbild")]
	public async Task Viewport_width_maps_onto_a_device_category(int viewportWidth, string expectedCategory)
	{
		PageViewEntity view = InMemoryPageViewReadStore.View(At(10, 0), "/", "only", sessionId: "s", viewportWidth: viewportWidth);
		GetPageViewSessions.Handler handler = new(new InMemoryPageViewReadStore(view));

		GetPageViewSessions.SessionListResult result = await handler.HandleListAsync(Query(), TestContext.Current.CancellationToken);

		Assert.Equal(expectedCategory, Assert.Single(result.Sessions).DeviceCategory);
	}
}
