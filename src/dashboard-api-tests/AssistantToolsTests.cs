using System.Text.Json;
using DashboardApi.Features.Assistant;
using DashboardApi.Tests.Fakes;
using dashboard_api.shared.entities;

namespace DashboardApi.Tests;

public sealed class AssistantToolsTests
{
	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public async Task Gutscheine_never_hand_the_buyer_name_to_the_model()
	{
		AssistantFixture fixture = new();
		fixture.Gutscheine.Add(new GutscheinEntity
		{
			Gutscheinnummer = "202512",
			RowKey = "202512",
			Kaufdatum = Now.AddDays(-10),
			Betrag = 80,
			VerkauftAn = "Maria Musterfrau"
		});

		AssistantTools.GutscheinListe result = await fixture.BuildTools().GutscheineAsync(nurOffen: false, Ct);

		// The whole point of projecting in the tool: the buyer's name must not exist anywhere in the payload.
		string json = JsonSerializer.Serialize(result);
		Assert.DoesNotContain("Musterfrau", json, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("VerkauftAn", json, StringComparison.OrdinalIgnoreCase);
		Assert.Equal("202512", Assert.Single(result.Gutscheine).Gutscheinnummer);
	}

	[Fact]
	public async Task Gutscheine_can_be_narrowed_to_the_open_ones_and_sum_them()
	{
		AssistantFixture fixture = new();
		fixture.Gutscheine.Add(InMemoryGutscheinStore.Gutschein("202501", Now.AddDays(-40)));
		fixture.Gutscheine.Add(InMemoryGutscheinStore.Gutschein("202502", Now.AddDays(-30), eingeloestAm: Now.AddDays(-2)));

		AssistantTools.GutscheinListe all = await fixture.BuildTools().GutscheineAsync(nurOffen: false, Ct);
		AssistantTools.GutscheinListe open = await fixture.BuildTools().GutscheineAsync(nurOffen: true, Ct);

		Assert.Equal(2, all.Anzahl);
		Assert.Equal(1, all.OffeneAnzahl);
		Assert.Equal("202501", Assert.Single(open.Gutscheine).Gutscheinnummer);
		Assert.Equal(50, open.OffenerBetrag);
		Assert.True(Assert.Single(open.Gutscheine).Offen);
	}

	[Fact]
	public async Task Alpakas_never_hand_a_signed_url_to_the_model()
	{
		AssistantFixture fixture = new();
		fixture.Alpakas.Add(AssistantFixture.Alpaka("alpaka-1", "Richard", "https://storage/alpakas/richard.jpg"));

		AssistantTools.AlpakaListe result = await fixture.BuildTools().AlpakasAsync(Ct);

		// A SAS link is useless to the model and is not something to hand out.
		string json = JsonSerializer.Serialize(result);
		Assert.DoesNotContain("sas", json, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("https://", json, StringComparison.Ordinal);
		Assert.Equal("Richard", Assert.Single(result.Alpakas).Name);
	}

	[Fact]
	public async Task Alpaka_detail_carries_the_events_but_no_image_url()
	{
		AssistantFixture fixture = new();
		fixture.Alpakas.Add(AssistantFixture.Alpaka("alpaka-1", "Richard", "https://storage/alpakas/richard.jpg"));
		fixture.Events.Add(AssistantFixture.Event("alpaka-1", "e-1", "Scheren", Now.AddDays(-30)));

		AssistantTools.AlpakaDetail? result = await fixture.BuildTools().AlpakaDetailAsync("alpaka-1", Ct);

		Assert.NotNull(result);
		Assert.Equal("Scheren", Assert.Single(result.Ereignisse).EventType);
		Assert.DoesNotContain("https://", JsonSerializer.Serialize(result), StringComparison.Ordinal);
	}

	[Fact]
	public async Task An_unknown_alpaka_returns_nothing_rather_than_throwing()
	{
		AssistantFixture fixture = new();

		Assert.Null(await fixture.BuildTools().AlpakaDetailAsync("gibt-es-nicht", Ct));
		Assert.Null(await fixture.BuildTools().AlpakaDetailAsync("   ", Ct));
	}

	[Fact]
	public async Task Ereignisse_are_capped_at_fifty_rows_and_say_so()
	{
		AssistantFixture fixture = new();
		fixture.AlpakaNames["alpaka-1"] = "Richard";
		for (int i = 0; i < 60; i++)
		{
			fixture.Events.Add(AssistantFixture.Event("alpaka-1", $"e-{i}", "Scheren", Now.AddDays(-i)));
		}

		AssistantTools.EreignisListe result = await fixture.BuildTools().EreignisseAsync(cancellationToken: Ct);

		Assert.Equal(60, result.Anzahl);
		Assert.Equal(AssistantTools.MaxRows, result.Ereignisse.Count);
		Assert.Equal(AssistantTools.TruncationHinweis, result.Hinweis);
	}

	[Fact]
	public async Task Ereignisse_are_filtered_to_the_requested_window()
	{
		AssistantFixture fixture = new();
		fixture.AlpakaNames["alpaka-1"] = "Richard";
		fixture.Events.Add(AssistantFixture.Event("alpaka-1", "e-mai", "Scheren", new DateTimeOffset(2025, 5, 20, 0, 0, 0, TimeSpan.Zero)));
		fixture.Events.Add(AssistantFixture.Event("alpaka-1", "e-juni", "Impfung", new DateTimeOffset(2025, 6, 12, 0, 0, 0, TimeSpan.Zero)));
		fixture.Events.Add(AssistantFixture.Event("alpaka-1", "e-juli", "Tierarzt", new DateTimeOffset(2025, 7, 3, 0, 0, 0, TimeSpan.Zero)));

		AssistantTools.EreignisListe result = await fixture.BuildTools().EreignisseAsync("2025-06-01", "2025-06-30", Ct);

		Assert.Equal("Impfung", Assert.Single(result.Ereignisse).EventType);
		Assert.Null(result.Hinweis);
		Assert.Equal(["Richard"], Assert.Single(result.Ereignisse).AlpakaNames);
	}

	[Fact]
	public async Task Nachrichten_statistik_counts_without_carrying_any_message_content()
	{
		AssistantFixture fixture = new();
		fixture.Messages.Add(AssistantFixture.Message(1));
		fixture.Messages.Add(AssistantFixture.Message(2, isSpam: true));

		AssistantTools.NachrichtenStatistik result = await fixture.BuildTools().NachrichtenStatistikAsync(28, Ct);

		Assert.Equal(2, result.Gesamt);
		Assert.Equal(1, result.Spam);
		Assert.Equal(1, result.Echt);

		// The bodies, names, addresses and phone numbers are not part of the tool surface at all.
		string json = JsonSerializer.Serialize(result);
		Assert.DoesNotContain("anna@example.at", json, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("wandern", json, StringComparison.OrdinalIgnoreCase);
		Assert.DoesNotContain("660", json, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Alte_nachrichten_returns_one_number_for_the_given_threshold()
	{
		AssistantFixture fixture = new();
		fixture.Messages.Add(AssistantFixture.Message(400));
		fixture.Messages.Add(AssistantFixture.Message(2));

		AssistantTools.AlteNachrichten result = await fixture.BuildTools().AlteNachrichtenAsync(180, Ct);

		Assert.Equal(180, result.TageSchwelle);
		Assert.Equal(1, result.Anzahl);
	}

	[Fact]
	public async Task Besucher_statistik_clamps_the_window_and_truncates_the_series()
	{
		AssistantFixture fixture = new();
		for (int i = 0; i < 40; i++)
		{
			fixture.PageViews.Add(InMemoryPageViewReadStore.View(Now.AddDays(-i * 7), "/", $"pv-{i}", sessionId: $"s-{i}", visitorId: $"v-{i}"));
		}

		// 900 days is over the 180 day cap and must be clamped rather than passed through.
		AssistantTools.BesucherStatistik result = await fixture.BuildTools().BesucherStatistikAsync(900, "gesamt", Ct);

		Assert.Equal(180, result.Tage);
		Assert.Equal("Woche", result.VerlaufsSchritt);
		Assert.True(result.Verlauf.Count <= AssistantTools.MaxSeriesBuckets);
	}

	[Fact]
	public async Task Sitzungen_liste_clamps_the_limit_to_twentyfive()
	{
		AssistantFixture fixture = new();
		for (int i = 0; i < 30; i++)
		{
			fixture.PageViews.Add(InMemoryPageViewReadStore.View(Now.AddHours(-i), "/", $"pv-{i}", sessionId: $"s-{i}", visitorId: $"v-{i}"));
		}

		AssistantTools tools = fixture.BuildTools();
		AssistantTools.SitzungenListe result = await tools.SitzungenListeAsync(28, 1, 999, Ct);

		Assert.True(result.Sitzungen.Count <= 25);
		Assert.Equal("""{"tage":28,"mindestSeiten":1,"limit":25}""", Assert.Single(tools.Invocations).Arguments);
	}

	[Fact]
	public void Heute_answers_in_the_vienna_timezone()
	{
		AssistantFixture fixture = new();
		AssistantTools tools = fixture.BuildTools();

		AssistantTools.HeutigesDatum result = tools.Heute();

		TimeZoneInfo vienna = TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
		string expected = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, vienna).ToString("yyyy-MM-dd");

		Assert.Equal(expected, result.Datum);
		Assert.Equal("Europe/Vienna", result.Zeitzone);
		Assert.Contains(result.Wochentag, (string[])["Montag", "Dienstag", "Mittwoch", "Donnerstag", "Freitag", "Samstag", "Sonntag"]);
		Assert.Equal("heute", Assert.Single(tools.Invocations).Tool);
	}

	[Fact]
	public void Clamp_drops_rows_until_the_payload_fits_the_byte_cap()
	{
		// Twenty rows is under the row cap, but 20 x 4 KB is well over the 32 KB byte cap.
		List<string> rows = [.. Enumerable.Range(0, 20).Select(_ => new string('x', 4096))];

		var (kept, hinweis) = AssistantTools.Clamp(rows, AssistantTools.MaxRows);

		Assert.Equal(AssistantTools.TruncationHinweis, hinweis);
		Assert.True(kept.Count < rows.Count);
		Assert.True(JsonSerializer.SerializeToUtf8Bytes(kept).Length <= AssistantTools.MaxBytes);
	}

	[Fact]
	public void Clamp_leaves_a_small_list_alone()
	{
		var (kept, hinweis) = AssistantTools.Clamp<string>(["a", "b"], AssistantTools.MaxRows);

		Assert.Equal(["a", "b"], kept);
		Assert.Null(hinweis);
	}

	[Fact]
	public void ClampTail_keeps_the_recent_end_of_a_series()
	{
		List<int> buckets = [.. Enumerable.Range(0, 40)];

		var (kept, hinweis) = AssistantTools.ClampTail(buckets, AssistantTools.MaxSeriesBuckets);

		Assert.Equal(AssistantTools.MaxSeriesBuckets, kept.Count);
		Assert.Equal(39, kept[^1]);
		Assert.Equal(AssistantTools.TruncationHinweis, hinweis);
	}

	[Fact]
	public async Task Every_tool_is_exposed_once_under_its_german_name()
	{
		AssistantFixture fixture = new();
		var names = fixture.BuildTools().All.Select(tool => tool.Name).ToList();

		Assert.Equal(
			[
				"besucher_statistik",
				"sitzungen_liste",
				"sitzung_detail",
				"nachrichten_statistik",
				"alte_nachrichten",
				"gutscheine",
				"alpakas",
				"alpaka_detail",
				"ereignisse",
				"heute"
			],
			names);
		Assert.Equal(names.Count, names.Distinct(StringComparer.Ordinal).Count());

		// Nothing on the surface writes, deletes or sends.
		Assert.DoesNotContain(names, name =>
			name.Contains("anlegen", StringComparison.Ordinal)
			|| name.Contains("loesch", StringComparison.Ordinal)
			|| name.Contains("send", StringComparison.Ordinal));

		await Task.CompletedTask;
	}
}
