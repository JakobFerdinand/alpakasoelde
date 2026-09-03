using DashboardApi.Features.PageViews;
using DashboardApi.Tests.Fakes;

namespace DashboardApi.Tests;

public sealed class GetPageViewStatsHandlerTests
{
	private static readonly DateTimeOffset WindowStart = new(2025, 6, 1, 0, 0, 0, TimeSpan.Zero);
	private static readonly DateTimeOffset InWindow = WindowStart.AddDays(1);

	private static InMemoryPageViewReadStore Store() => new(
		InMemoryPageViewReadStore.View(InWindow, "/", "a", sessionId: "s1", visitorId: "v1", referrerHost: "google.at", navigationType: "navigate"),
		InMemoryPageViewReadStore.View(InWindow, "/", "b", sessionId: "s1", visitorId: "v1", referrerHost: "alpakasoelde.at", navigationType: "reload"),
		InMemoryPageViewReadStore.View(InWindow, "/alpakas/", "c", sessionId: "s2", visitorId: "v2", viewportWidth: 500,
			referrerHost: "lemon-hill-0ebd24003-abc.westeurope.6.azurestaticapps.net"),
		// Same origin as the first view but shouted, so grouping has to be case-insensitive.
		InMemoryPageViewReadStore.View(InWindow, "/produkte", "d", sessionId: "s3", visitorId: "v2", viewportWidth: 1920,
			referrerHost: "GOOGLE.AT", navigationType: "navigate"),
		InMemoryPageViewReadStore.View(WindowStart.AddDays(-30), "/", "old", sessionId: "s0", visitorId: "v0"));

	private static Task<GetPageViewStats.Result> Handle(string groupBy = "total")
		=> new GetPageViewStats.Handler(Store()).HandleAsync(
			new GetPageViewStats.Query(7, "week", groupBy, WindowStart),
			TestContext.Current.CancellationToken);

	[Fact]
	public async Task Totals_count_only_views_inside_the_window_and_normalise_the_path()
	{
		GetPageViewStats.Result result = await Handle();

		Assert.Equal(4, result.Total);
		// "/alpakas/" and "/alpakas" are the same page.
		Assert.Equal(3, result.UniquePaths);
		Assert.Equal(
			[("/", 2), ("/alpakas", 1), ("/produkte", 1)],
			result.TopPaths.Select(p => (p.Path, p.Count)));
	}

	[Fact]
	public async Task Devices_are_ordered_by_count_and_then_by_screen_size()
	{
		GetPageViewStats.Result result = await Handle();

		Assert.Equal(
			[("Laptop", 2), ("Mobil", 1), ("Breitbild", 1)],
			result.Devices.Select(d => (d.Category, d.Count)));
	}

	[Fact]
	public async Task Own_and_preview_hosts_are_not_reported_as_referrers()
	{
		GetPageViewStats.Result result = await Handle();

		// alpakasoelde.at and the lemon-hill preview host are internal; the two
		// google.at spellings collapse into one lowercased origin.
		Assert.Equal([("google.at", 2)], result.Origins.Select(o => (o.Domain, o.Count)));
	}

	[Fact]
	public async Task Sessions_visitors_and_navigation_types_are_counted_distinctly()
	{
		GetPageViewStats.Result result = await Handle();

		Assert.Equal(3, result.Sessions);
		Assert.Equal(2, result.Visitors);
		Assert.Equal(
			[("navigate", 2), ("reload", 1)],
			result.Navigations.Select(n => (n.Type, n.Count)));
	}

	[Fact]
	public async Task The_series_spans_the_window_and_accounts_for_every_view_in_it()
	{
		GetPageViewStats.Result result = await Handle();

		Assert.Equal("week", result.Granularity);
		Assert.Equal("total", result.GroupBy);
		Assert.All(result.Series, bucket => Assert.Null(bucket.Group));
		Assert.Equal(result.Total, result.Series.Sum(b => b.Count));
		// Weekly buckets start on Mondays and are contiguous.
		List<DateTime> periods = [.. result.Series.Select(b => DateTime.Parse(b.Period))];
		Assert.All(periods, period => Assert.Equal(DayOfWeek.Monday, period.DayOfWeek));
		Assert.Equal(periods.OrderBy(p => p), periods);
	}

	[Fact]
	public async Task Grouping_by_device_emits_a_bucket_for_every_category_in_every_period()
	{
		GetPageViewStats.Result result = await Handle(groupBy: "device");

		string[] categories = ["Mobil", "Tablet", "Laptop", "Breitbild"];
		Assert.Equal(result.Total, result.Series.Sum(b => b.Count));
		foreach (IGrouping<string?, GetPageViewStats.Bucket> period in result.Series.GroupBy(b => b.Period))
		{
			Assert.Equal(categories, period.Select(b => b.Group));
		}
	}

	[Fact]
	public async Task The_audience_series_counts_distinct_visitors_and_sessions_per_period()
	{
		GetPageViewStats.Result result = await Handle();

		GetPageViewStats.AudienceBucket populated = Assert.Single(result.AudienceSeries, b => b.Sessions > 0);
		Assert.Equal(2, populated.Visitors);
		Assert.Equal(3, populated.Sessions);
		Assert.Equal(result.Series.Select(b => b.Period), result.AudienceSeries.Select(b => b.Period));
	}
}
