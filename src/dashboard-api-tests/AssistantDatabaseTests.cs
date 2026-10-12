using System.Text.Json;
using DashboardApi.Features.Assistant;
using DashboardApi.Tests.Fakes;
using dashboard_api.shared.entities;

namespace DashboardApi.Tests;

public sealed class AssistantDatabaseTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	private static Task<AssistantTools.DatenAbfrage> Ask(AssistantFixture fixture, string sql) =>
		fixture.BuildTools().DatenAbfrageAsync(sql, Ct);

	[Fact]
	public async Task Page_views_can_be_counted_per_weekday_in_vienna_time()
	{
		AssistantFixture fixture = new();
		// 2026-03-01 23:30 UTC is already Monday 00:30 in Vienna.
		fixture.PageViews.Add(InMemoryPageViewReadStore.View(new DateTimeOffset(2026, 3, 1, 23, 30, 0, TimeSpan.Zero), "/", "a"));
		fixture.PageViews.Add(InMemoryPageViewReadStore.View(new DateTimeOffset(2026, 3, 2, 10, 0, 0, TimeSpan.Zero), "/", "b"));
		fixture.PageViews.Add(InMemoryPageViewReadStore.View(new DateTimeOffset(2026, 3, 4, 10, 0, 0, TimeSpan.Zero), "/", "c"));

		AssistantTools.DatenAbfrage result = await Ask(fixture,
			"SELECT wochentag, COUNT(*) AS aufrufe FROM seitenaufrufe GROUP BY wochentag_nr ORDER BY wochentag_nr");

		Assert.Null(result.Fehler);
		Assert.Equal(["wochentag", "aufrufe"], result.Spalten);
		Assert.Equal(2, result.Zeilen.Count);
		Assert.Equal("Montag", result.Zeilen[0][0]);
		Assert.Equal(2L, result.Zeilen[0][1]);
		Assert.Equal("Mittwoch", result.Zeilen[1][0]);
	}

	[Fact]
	public async Task Internal_referrers_are_not_an_origin_and_paths_are_normalised()
	{
		AssistantFixture fixture = new();
		fixture.PageViews.Add(InMemoryPageViewReadStore.View(DateTimeOffset.UtcNow, "/wandern/", "a", referrerHost: "alpakasoelde.at"));
		fixture.PageViews.Add(InMemoryPageViewReadStore.View(DateTimeOffset.UtcNow, "/", "b", referrerHost: "Google.com"));

		AssistantTools.DatenAbfrage result = await Ask(fixture, "SELECT pfad, herkunft FROM seitenaufrufe ORDER BY pfad");

		Assert.Equal("/", result.Zeilen[0][0]);
		Assert.Equal("google.com", result.Zeilen[0][1]);
		Assert.Equal("/wandern", result.Zeilen[1][0]);
		Assert.Null(result.Zeilen[1][1]);
	}

	[Fact]
	public async Task Private_data_never_enters_the_database()
	{
		AssistantFixture fixture = new();
		fixture.Messages.Add(AssistantFixture.Message(daysAgo: 1));
		fixture.Gutscheine.Add(new GutscheinEntity
		{
			Gutscheinnummer = "202512",
			RowKey = "202512",
			Kaufdatum = DateTimeOffset.UtcNow,
			Betrag = 80,
			VerkauftAn = "Maria Musterfrau"
		});
		fixture.Alpakas.Add(AssistantFixture.Alpaka("alpaka-1", "Richard", "https://storage/alpakas/richard.jpg"));

		StringWriter dump = new();
		foreach (string table in new[] { "nachrichten", "gutscheine", "alpakas" })
		{
			AssistantTools.DatenAbfrage all = await Ask(fixture, $"SELECT * FROM {table}");
			Assert.Null(all.Fehler);
			dump.WriteLine(JsonSerializer.Serialize(all));
		}

		string text = dump.ToString();
		Assert.DoesNotContain("Musterfrau", text);
		Assert.DoesNotContain("anna@example.at", text);
		Assert.DoesNotContain("wandern", text);
		Assert.DoesNotContain("+43", text);
		Assert.DoesNotContain("https://", text);
	}

	[Fact]
	public async Task Events_carry_the_alpaka_name_and_vouchers_know_if_they_are_open()
	{
		AssistantFixture fixture = new();
		fixture.Alpakas.Add(AssistantFixture.Alpaka("alpaka-1", "Richard"));
		fixture.Events.Add(AssistantFixture.Event("alpaka-1", "e-1", "Scheren", new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero), cost: 30));
		fixture.Gutscheine.Add(InMemoryGutscheinStore.Gutschein("202501", DateTimeOffset.UtcNow.AddDays(-9)));

		AssistantTools.DatenAbfrage events = await Ask(fixture, "SELECT alpaka_name, typ, monat, kosten FROM ereignisse");
		AssistantTools.DatenAbfrage vouchers = await Ask(fixture, "SELECT SUM(betrag) FROM gutscheine WHERE offen = 1");

		Assert.Equal(["Richard", "Scheren", "2026-05", 30.0], events.Zeilen.Single());
		Assert.Equal(50.0, vouchers.Zeilen.Single().Single());
	}

	[Theory]
	[InlineData("DELETE FROM seitenaufrufe")]
	[InlineData("DROP TABLE alpakas")]
	[InlineData("INSERT INTO alpakas VALUES ('x', 'y', 'z')")]
	[InlineData("PRAGMA query_only = OFF")]
	[InlineData("ATTACH DATABASE 'x.db' AS x")]
	[InlineData("SELECT 1; DROP TABLE alpakas")]
	[InlineData("   ")]
	public async Task Anything_but_a_single_select_is_refused(string sql)
	{
		AssistantFixture fixture = new();
		fixture.Alpakas.Add(AssistantFixture.Alpaka("alpaka-1", "Richard"));

		AssistantTools.DatenAbfrage result = await Ask(fixture, sql);

		Assert.NotNull(result.Fehler);
		Assert.Empty(result.Zeilen);
		Assert.Equal("Richard", (await Ask(fixture, "SELECT name FROM alpakas")).Zeilen.Single().Single());
	}

	[Fact]
	public async Task A_select_that_hides_a_write_in_a_cte_is_denied_by_the_authorizer()
	{
		AssistantFixture fixture = new();
		fixture.Alpakas.Add(AssistantFixture.Alpaka("alpaka-1", "Richard"));

		AssistantTools.DatenAbfrage result = await Ask(fixture, "WITH x AS (SELECT 1) DELETE FROM alpakas");

		Assert.NotNull(result.Fehler);
		Assert.Equal("Richard", (await Ask(fixture, "SELECT name FROM alpakas")).Zeilen.Single().Single());
	}

	[Fact]
	public async Task A_semicolon_inside_a_string_literal_is_fine()
	{
		AssistantFixture fixture = new();

		AssistantTools.DatenAbfrage result = await Ask(fixture, "SELECT 'a;b' AS text;");

		Assert.Null(result.Fehler);
		Assert.Equal("a;b", result.Zeilen.Single().Single());
	}

	[Fact]
	public async Task A_mistake_comes_back_as_text_so_the_model_can_correct_it()
	{
		AssistantTools.DatenAbfrage result = await Ask(new AssistantFixture(), "SELECT gibt_es_nicht FROM alpakas");

		Assert.Contains("gibt_es_nicht", result.Fehler);
	}

	[Fact]
	public async Task A_long_result_is_cut_and_flagged()
	{
		AssistantFixture fixture = new();
		for (int i = 0; i < 80; i++)
		{
			fixture.Alpakas.Add(AssistantFixture.Alpaka($"alpaka-{i}", $"Tier {i}"));
		}

		AssistantTools.DatenAbfrage result = await Ask(fixture, "SELECT id FROM alpakas");

		Assert.Equal(AssistantTools.MaxRows, result.Zeilen.Count);
		Assert.Equal(AssistantTools.TruncationHinweis, result.Hinweis);
	}

	[Fact]
	public async Task A_runaway_query_is_interrupted()
	{
		AssistantTools.DatenAbfrage result = await Ask(new AssistantFixture(),
			"WITH RECURSIVE n(i) AS (SELECT 1 UNION ALL SELECT i + 1 FROM n) SELECT COUNT(*) FROM n");

		Assert.NotNull(result.Fehler);
	}
}
