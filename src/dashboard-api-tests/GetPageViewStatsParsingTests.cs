using DashboardApi.Features.PageViews;

namespace DashboardApi.Tests;

public sealed class GetPageViewStatsParsingTests
{
	private static GetPageViewStats.Query Parse(params (string Key, string Value)[] entries)
	{
		var query = new System.Collections.Specialized.NameValueCollection();
		foreach ((string key, string value) in entries)
		{
			query[key] = value;
		}

		// Missing keys read as null, exactly like a truncated query string.
		return GetPageViewStats.Parse(
			query["days"],
			query["from"],
			query["to"],
			query["granularity"],
			query["groupBy"]);
	}

	[Fact]
	public void Days_are_capped_at_the_180_day_table_lookback()
	{
		Assert.Equal(180, Parse(("days", "999")).Days);
		Assert.Equal(90, Parse(("days", "90")).Days);
	}

	[Fact]
	public void Missing_or_non_positive_days_fall_back_to_the_default_window()
	{
		Assert.Equal(28, Parse(("days", "abc")).Days);
		Assert.Equal(28, Parse(("days", "0")).Days);
		Assert.Equal(28, Parse().Days);
	}

	[Fact]
	public void Hour_granularity_is_capped_at_28_days_but_kept_when_it_fits()
	{
		GetPageViewStats.Query over = Parse(("days", "90"), ("granularity", "hour"));
		GetPageViewStats.Query within = Parse(("days", "7"), ("granularity", "hour"));

		Assert.Equal(28, over.Days);
		Assert.Equal("hour", over.Granularity);
		Assert.Equal(7, within.Days);
		Assert.Equal("hour", within.Granularity);
	}

	[Fact]
	public void A_valid_date_range_overrides_days_and_anchors_the_window_start()
	{
		GetPageViewStats.Query range = Parse(("days", "7"), ("from", "2025-01-01"), ("to", "2025-01-10"));

		Assert.Equal(10, range.Days);
		Assert.Equal(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), range.WindowStart);
	}

	[Fact]
	public void A_date_range_wider_than_the_table_lookback_is_capped()
	{
		Assert.Equal(180, Parse(("from", "2024-01-01"), ("to", "2025-01-01")).Days);
	}

	[Fact]
	public void Broken_or_reversed_date_bounds_are_ignored_and_days_apply()
	{
		GetPageViewStats.Query reversed = Parse(("days", "7"), ("from", "2025-01-10"), ("to", "2025-01-01"));
		GetPageViewStats.Query unparseable = Parse(("days", "7"), ("from", "not-a-date"), ("to", "2025-01-01"));

		Assert.Equal(7, reversed.Days);
		Assert.Null(reversed.WindowStart);
		Assert.Equal(7, unparseable.Days);
		Assert.Null(unparseable.WindowStart);
	}

	[Theory]
	[InlineData("quartal")]
	[InlineData("MONTH")]
	[InlineData("")]
	public void An_unknown_granularity_falls_back_to_week(string granularity)
	{
		Assert.Equal("week", Parse(("granularity", granularity)).Granularity);
	}

	[Fact]
	public void Known_granularity_and_group_by_are_kept_and_unknown_group_by_defaults_to_path()
	{
		GetPageViewStats.Query kept = Parse(("granularity", "day"), ("groupBy", "device"));
		GetPageViewStats.Query unmentioned = Parse(("granularity", "day"));

		Assert.Equal("day", kept.Granularity);
		Assert.Equal("device", kept.GroupBy);
		Assert.Equal("path", unmentioned.GroupBy);
	}
}
