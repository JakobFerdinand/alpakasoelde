using System.ComponentModel;
using System.Text.Json;
using DashboardApi.Features.Alpakas;
using DashboardApi.Features.Gutscheine;
using DashboardApi.Features.Messages;
using DashboardApi.Features.PageViews;
using Microsoft.Extensions.AI;
using EventsFeature = DashboardApi.Features.Events.Events;

namespace DashboardApi.Features.Assistant;

/// <summary>
/// The assistant's whole reach into the app's data: one method per tool, each a thin wrapper around an
/// existing read handler. Nothing here writes, deletes, mails or fetches a URL, and every result is clamped
/// before it can reach the model.
/// </summary>
public sealed class AssistantTools(
	GetPageViewStats.Handler pageViewStats,
	GetPageViewSessions.Handler pageViewSessions,
	GetMessageStats.Handler messageStats,
	GetOldMessageCount.Handler oldMessageCount,
	GetGutscheine.Handler gutscheine,
	GetAlpakas.Handler alpakas,
	GetAlpakaById.Handler alpakaById,
	EventsFeature.GetHandler events)
{
	/// <summary>Rows handed to the model per tool call.</summary>
	public const int MaxRows = 50;

	/// <summary>Serialised bytes handed to the model per tool call.</summary>
	public const int MaxBytes = 32 * 1024;

	/// <summary>Buckets of a time series handed to the model.</summary>
	public const int MaxSeriesBuckets = 26;

	public const string TruncationHinweis = "gekürzt";

	private static readonly TimeZoneInfo ViennaTimeZone = ResolveViennaTimeZone();

	private readonly List<Assistant.ToolTrace> _invocations = [];

	/// <summary>What ran during this request, in call order — the trace shown under the answer.</summary>
	public IReadOnlyList<Assistant.ToolTrace> Invocations => _invocations;

	/// <summary>The tool list handed to the agent.</summary>
	public IList<AITool> All =>
	[
		AIFunctionFactory.Create(BesucherStatistikAsync, name: "besucher_statistik"),
		AIFunctionFactory.Create(SitzungenListeAsync, name: "sitzungen_liste"),
		AIFunctionFactory.Create(SitzungDetailAsync, name: "sitzung_detail"),
		AIFunctionFactory.Create(NachrichtenStatistikAsync, name: "nachrichten_statistik"),
		AIFunctionFactory.Create(AlteNachrichtenAsync, name: "alte_nachrichten"),
		AIFunctionFactory.Create(GutscheineAsync, name: "gutscheine"),
		AIFunctionFactory.Create(AlpakasAsync, name: "alpakas"),
		AIFunctionFactory.Create(AlpakaDetailAsync, name: "alpaka_detail"),
		AIFunctionFactory.Create(EreignisseAsync, name: "ereignisse"),
		AIFunctionFactory.Create(Heute, name: "heute"),
	];

	[Description("""
		Liefert die Website-Statistik für die letzten Tage: Seitenaufrufe, Sitzungen, eindeutige Besucher,
		die meistbesuchten Pfade, Geräteklassen, externe Herkunftsseiten, Navigationsarten und einen Zeitverlauf.
		Nutze dieses Werkzeug für alle Fragen zu Besuchern, Aufrufen, beliebten Seiten und Traffic-Quellen.
		""")]
	public async Task<BesucherStatistik> BesucherStatistikAsync(
		[Description("Größe des Zeitfensters in Tagen, gezählt ab heute rückwärts. Erlaubt sind 1 bis 180.")] int tage,
		[Description("Aufschlüsselung des Zeitverlaufs: 'gesamt', 'pfad', 'gerät' oder 'herkunft'.")] string gruppierung,
		CancellationToken cancellationToken = default)
	{
		int days = Math.Clamp(tage, 1, 180);
		string groupBy = gruppierung?.Trim().ToLowerInvariant() switch
		{
			"gesamt" => "total",
			"gerät" or "geraet" or "gerat" => "device",
			"herkunft" => "origin",
			_ => "path",
		};
		// Daily buckets stay readable for short windows; anything longer would blow past the bucket cap.
		string granularity = days <= 14 ? "day" : "week";

		Record("besucher_statistik", new { tage = days, gruppierung = groupBy });

		GetPageViewStats.Result result = await pageViewStats
			.HandleAsync(new GetPageViewStats.Query(days, granularity, groupBy), cancellationToken)
			.ConfigureAwait(false);

		// The tail of the series is the recent end, which is what a question is almost always about.
		var (series, seriesHinweis) = ClampTail(result.Series, MaxSeriesBuckets);
		var (topPaths, pathHinweis) = Clamp(result.TopPaths, MaxRows);
		var (origins, originHinweis) = Clamp(result.Origins, MaxRows);

		return new BesucherStatistik(
			days,
			result.Total,
			result.Sessions,
			result.Visitors,
			result.UniquePaths,
			topPaths,
			result.Devices,
			origins,
			result.Navigations,
			series,
			granularity == "day" ? "Tag" : "Woche",
			FirstHinweis(seriesHinweis, pathHinweis, originHinweis));
	}

	[Description("""
		Listet einzelne Website-Sitzungen als Kurzfassung: Beginn, Dauer, Anzahl der Seiten, Einstiegs- und
		Ausstiegspfad, Herkunft und Geräteklasse. Nutze dieses Werkzeug, wenn nach einzelnen Besuchen oder
		nach dem Verhalten von Besuchern gefragt wird, nicht für Gesamtzahlen.
		""")]
	public async Task<SitzungenListe> SitzungenListeAsync(
		[Description("Größe des Zeitfensters in Tagen, gezählt ab heute rückwärts. Erlaubt sind 1 bis 180.")] int tage,
		[Description("Nur Sitzungen mit mindestens so vielen Seitenaufrufen. Mindestens 1.")] int mindestSeiten,
		[Description("Höchstzahl der zurückgegebenen Sitzungen. Erlaubt sind 1 bis 25.")] int limit,
		CancellationToken cancellationToken = default)
	{
		int days = Math.Clamp(tage, 1, 180);
		int minPages = Math.Clamp(mindestSeiten, 1, 100);
		int max = Math.Clamp(limit, 1, 25);

		Record("sitzungen_liste", new { tage = days, mindestSeiten = minPages, limit = max });

		GetPageViewSessions.SessionListResult result = await pageViewSessions
			.HandleListAsync(new GetPageViewSessions.ListQuery(days, minPages, max, null, null), cancellationToken)
			.ConfigureAwait(false);

		var (sessions, hinweis) = Clamp(result.Sessions, max);
		return new SitzungenListe(
			result.WindowDays,
			sessions,
			// The handler truncates on its own too, and that is just as much a "not everything" signal.
			FirstHinweis(hinweis, result.Truncated ? TruncationHinweis : null));
	}

	[Description("""
		Liefert den vollständigen Verlauf einer einzelnen Sitzung: jede aufgerufene Seite in Reihenfolge mit
		Verweildauer. Die Sitzungs-ID stammt aus 'sitzungen_liste'.
		""")]
	public async Task<SitzungDetail?> SitzungDetailAsync(
		[Description("Die ID der Sitzung, wie sie 'sitzungen_liste' zurückgibt.")] string sitzungsId,
		CancellationToken cancellationToken = default)
	{
		string id = sitzungsId?.Trim() ?? string.Empty;
		Record("sitzung_detail", new { sitzungsId = id });

		if (id.Length == 0 || id.Length > 64)
		{
			return null;
		}

		GetPageViewSessions.SessionDetailResult? result = await pageViewSessions
			.HandleDetailAsync(id, cancellationToken)
			.ConfigureAwait(false);

		if (result is null)
		{
			return null;
		}

		var (sessionEvents, hinweis) = Clamp(result.Events, MaxRows);
		return new SitzungDetail(result.Summary, sessionEvents, hinweis);
	}

	[Description("""
		Liefert Kennzahlen zu den Kontaktanfragen: Gesamtzahl, als Spam eingestufte, echte und unbeantwortet
		alte Anfragen, dazu ein Wochenverlauf. Gibt ausdrücklich keine Namen, E-Mail-Adressen, Telefonnummern
		oder Nachrichtentexte zurück — diese Daten sind für den Assistenten nicht zugänglich.
		""")]
	public async Task<NachrichtenStatistik> NachrichtenStatistikAsync(
		[Description("Größe des Zeitfensters in Tagen, gezählt ab heute rückwärts. Erlaubt sind 1 bis 365.")] int tage,
		CancellationToken cancellationToken = default)
	{
		int days = Math.Clamp(tage, 1, 365);
		Record("nachrichten_statistik", new { tage = days });

		GetMessageStats.Result result = await messageStats
			.HandleAsync(new GetMessageStats.Query(days), cancellationToken)
			.ConfigureAwait(false);

		var (series, hinweis) = ClampTail(result.Series, MaxSeriesBuckets);
		return new NachrichtenStatistik(days, result.Total, result.Spam, result.Legit, result.OldCount, series, hinweis);
	}

	[Description("""
		Zählt die Kontaktanfragen, die älter als eine gegebene Anzahl Tage sind. Nutze dieses Werkzeug für
		Fragen nach liegengebliebenen oder alten Anfragen.
		""")]
	public async Task<AlteNachrichten> AlteNachrichtenAsync(
		[Description("Altersschwelle in Tagen. Erlaubt sind 1 bis 3650.")] int tageSchwelle,
		CancellationToken cancellationToken = default)
	{
		int days = Math.Clamp(tageSchwelle, 1, 3650);
		Record("alte_nachrichten", new { tageSchwelle = days });

		GetOldMessageCount.Result result = await oldMessageCount
			.HandleAsync(new GetOldMessageCount.Query(TimeSpan.FromDays(days)), cancellationToken)
			.ConfigureAwait(false);

		return new AlteNachrichten(days, result.Count);
	}

	[Description("""
		Listet die Gutscheine mit Nummer, Kaufdatum, Betrag und Einlösedatum, dazu die Summen der offenen und
		eingelösten Beträge. Der Name des Käufers wird aus Datenschutzgründen nicht zurückgegeben.
		""")]
	public async Task<GutscheinListe> GutscheineAsync(
		[Description("true liefert nur noch nicht eingelöste Gutscheine, false liefert alle.")] bool nurOffen,
		CancellationToken cancellationToken = default)
	{
		Record("gutscheine", new { nurOffen });

		IReadOnlyList<GetGutscheine.GutscheinResult> all = await gutscheine
			.HandleAsync(new GetGutscheine.Query(), cancellationToken)
			.ConfigureAwait(false);

		// Projected here rather than filtered later: VerkauftAn must never reach the model.
		List<GutscheinInfo> vouchers = all
			.Where(voucher => !nurOffen || voucher.EingeloestAm is null)
			.Select(voucher => new GutscheinInfo(
				voucher.Gutscheinnummer,
				voucher.Kaufdatum,
				voucher.Betrag,
				voucher.EingeloestAm,
				voucher.EingeloestAm is null))
			.ToList();

		int openCount = vouchers.Count(voucher => voucher.Offen);
		double openSum = vouchers.Where(voucher => voucher.Offen).Sum(voucher => voucher.Betrag);

		var (rows, hinweis) = Clamp(vouchers, MaxRows);
		return new GutscheinListe(vouchers.Count, openCount, openSum, rows, hinweis);
	}

	[Description("""
		Listet alle Alpakas der Farm mit ID, Name und Geburtsdatum. Nutze dieses Werkzeug, um von einem Namen
		auf die Alpaka-ID zu kommen, die 'alpaka_detail' braucht.
		""")]
	public async Task<AlpakaListe> AlpakasAsync(CancellationToken cancellationToken = default)
	{
		Record("alpakas", new { });

		IReadOnlyList<GetAlpakas.AlpakaListItem> all = await alpakas
			.HandleAsync(new GetAlpakas.Query(), cancellationToken)
			.ConfigureAwait(false);

		// ImageUrl is a short-lived signed SAS link: useless to the model and not something to hand out.
		List<AlpakaInfo> animals = all
			.Select(alpaka => new AlpakaInfo(alpaka.Id, alpaka.Name, alpaka.Geburtsdatum))
			.ToList();

		var (rows, hinweis) = Clamp(animals, MaxRows);
		return new AlpakaListe(animals.Count, rows, hinweis);
	}

	[Description("""
		Liefert ein einzelnes Alpaka samt seiner Ereignisse (Scheren, Impfung, Tierarzt und Ähnliches) mit
		Datum, Kommentar und Kosten. Die Alpaka-ID stammt aus 'alpakas'.
		""")]
	public async Task<AlpakaDetail?> AlpakaDetailAsync(
		[Description("Die ID des Alpakas, wie sie 'alpakas' zurückgibt.")] string alpakaId,
		CancellationToken cancellationToken = default)
	{
		string id = alpakaId?.Trim() ?? string.Empty;
		Record("alpaka_detail", new { alpakaId = id });

		if (id.Length == 0)
		{
			return null;
		}

		GetAlpakaById.Result? result = await alpakaById
			.HandleAsync(new GetAlpakaById.Query(id), cancellationToken)
			.ConfigureAwait(false);

		if (result is null)
		{
			return null;
		}

		var (alpakaEvents, hinweis) = Clamp(result.Events, MaxRows);
		return new AlpakaDetail(result.Id, result.Name, result.Geburtsdatum, alpakaEvents, hinweis);
	}

	[Description("""
		Listet die Ereignisse der Farm (Scheren, Impfung, Tierarzt und Ähnliches) mit Datum, Kommentar, Kosten
		und den beteiligten Alpakas, optional auf einen Zeitraum eingeschränkt. Nutze dieses Werkzeug für
		Fragen wie 'Wann war das letzte Scheren?' oder 'Was ist im Juni passiert?'.
		""")]
	public async Task<EreignisListe> EreignisseAsync(
		[Description("Frühestes Datum im Format JJJJ-MM-TT. Leer lassen, um nicht nach unten einzuschränken.")] string? vonDatum = null,
		[Description("Spätestes Datum im Format JJJJ-MM-TT. Leer lassen, um nicht nach oben einzuschränken.")] string? bisDatum = null,
		CancellationToken cancellationToken = default)
	{
		DateOnly? from = ParseDate(vonDatum);
		DateOnly? to = ParseDate(bisDatum);
		Record("ereignisse", new { vonDatum = from?.ToString("yyyy-MM-dd"), bisDatum = to?.ToString("yyyy-MM-dd") });

		IReadOnlyList<EventsFeature.EventResult> all = await events
			.HandleAsync(new EventsFeature.GetQuery(), cancellationToken)
			.ConfigureAwait(false);

		// The handler loads the whole table and sorts newest first; the window is applied here.
		List<EventsFeature.EventResult> filtered = all
			.Where(e => DateOnly.TryParse(e.EventDate, out DateOnly date)
				&& (from is null || date >= from)
				&& (to is null || date <= to))
			.ToList();

		var (rows, hinweis) = Clamp(filtered, MaxRows);
		return new EreignisListe(filtered.Count, rows, hinweis);
	}

	[Description("""
		Gibt das heutige Datum in der Zeitzone Europe/Vienna zurück. Rufe dieses Werkzeug immer zuerst auf,
		wenn die Frage relative Zeitangaben wie 'letzte Woche', 'im Juni' oder 'heuer' enthält.
		""")]
	public HeutigesDatum Heute()
	{
		Record("heute", new { });

		DateTimeOffset now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, ViennaTimeZone);
		return new HeutigesDatum(
			now.ToString("yyyy-MM-dd"),
			GermanWeekday(now.DayOfWeek),
			now.ToString("HH:mm"),
			"Europe/Vienna");
	}

	private void Record(string tool, object arguments) =>
		_invocations.Add(new Assistant.ToolTrace(tool, JsonSerializer.Serialize(arguments)));

	/// <summary>Caps a list at <paramref name="maxRows"/> rows and at <see cref="MaxBytes"/> serialised bytes.</summary>
	public static (IReadOnlyList<T> Rows, string? Hinweis) Clamp<T>(IReadOnlyList<T> rows, int maxRows)
	{
		string? hinweis = null;
		List<T> kept = [.. rows];

		if (kept.Count > maxRows)
		{
			kept.RemoveRange(maxRows, kept.Count - maxRows);
			hinweis = TruncationHinweis;
		}

		while (kept.Count > 0 && JsonSerializer.SerializeToUtf8Bytes(kept).Length > MaxBytes)
		{
			kept.RemoveAt(kept.Count - 1);
			hinweis = TruncationHinweis;
		}

		return (kept, hinweis);
	}

	/// <summary>Same caps as <see cref="Clamp"/>, but keeps the tail — the recent end of a time series.</summary>
	public static (IReadOnlyList<T> Rows, string? Hinweis) ClampTail<T>(IReadOnlyList<T> rows, int maxRows)
	{
		string? hinweis = null;
		List<T> kept = [.. rows];

		if (kept.Count > maxRows)
		{
			kept.RemoveRange(0, kept.Count - maxRows);
			hinweis = TruncationHinweis;
		}

		while (kept.Count > 0 && JsonSerializer.SerializeToUtf8Bytes(kept).Length > MaxBytes)
		{
			kept.RemoveAt(0);
			hinweis = TruncationHinweis;
		}

		return (kept, hinweis);
	}

	private static string? FirstHinweis(params string?[] hinweise) =>
		Array.Find(hinweise, hinweis => hinweis is not null);

	private static DateOnly? ParseDate(string? value) =>
		DateOnly.TryParse(value, out DateOnly parsed) ? parsed : null;

	private static string GermanWeekday(DayOfWeek day) => day switch
	{
		DayOfWeek.Monday => "Montag",
		DayOfWeek.Tuesday => "Dienstag",
		DayOfWeek.Wednesday => "Mittwoch",
		DayOfWeek.Thursday => "Donnerstag",
		DayOfWeek.Friday => "Freitag",
		DayOfWeek.Saturday => "Samstag",
		_ => "Sonntag",
	};

	private static TimeZoneInfo ResolveViennaTimeZone()
	{
		// The IANA id works on Linux and, through ICU, on Windows too; the Windows id is the belt-and-braces path.
		try
		{
			return TimeZoneInfo.FindSystemTimeZoneById("Europe/Vienna");
		}
		catch (TimeZoneNotFoundException)
		{
			return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time");
		}
	}

	public sealed record BesucherStatistik(
		int Tage,
		int Seitenaufrufe,
		int Sitzungen,
		int Besucher,
		int UnterschiedlichePfade,
		IReadOnlyList<GetPageViewStats.PathCount> TopPfade,
		IReadOnlyList<GetPageViewStats.DeviceCount> Geraete,
		IReadOnlyList<GetPageViewStats.OriginCount> Herkunft,
		IReadOnlyList<GetPageViewStats.NavigationCount> Navigationsarten,
		IReadOnlyList<GetPageViewStats.Bucket> Verlauf,
		string VerlaufsSchritt,
		string? Hinweis);

	public sealed record SitzungenListe(
		int Tage,
		IReadOnlyList<GetPageViewSessions.SessionSummary> Sitzungen,
		string? Hinweis);

	public sealed record SitzungDetail(
		GetPageViewSessions.SessionSummary Zusammenfassung,
		IReadOnlyList<GetPageViewSessions.SessionEvent> Verlauf,
		string? Hinweis);

	public sealed record NachrichtenStatistik(
		int Tage,
		int Gesamt,
		int Spam,
		int Echt,
		int Alt,
		IReadOnlyList<GetMessageStats.PeriodBucket> Verlauf,
		string? Hinweis);

	public sealed record AlteNachrichten(int TageSchwelle, int Anzahl);

	public sealed record GutscheinInfo(
		string Gutscheinnummer,
		string Kaufdatum,
		double Betrag,
		string? EingeloestAm,
		bool Offen);

	public sealed record GutscheinListe(
		int Anzahl,
		int OffeneAnzahl,
		double OffenerBetrag,
		IReadOnlyList<GutscheinInfo> Gutscheine,
		string? Hinweis);

	public sealed record AlpakaInfo(string Id, string Name, string Geburtsdatum);

	public sealed record AlpakaListe(int Anzahl, IReadOnlyList<AlpakaInfo> Alpakas, string? Hinweis);

	public sealed record AlpakaDetail(
		string Id,
		string Name,
		string Geburtsdatum,
		IReadOnlyList<GetAlpakaById.EventResult> Ereignisse,
		string? Hinweis);

	public sealed record EreignisListe(int Anzahl, IReadOnlyList<EventsFeature.EventResult> Ereignisse, string? Hinweis);

	public sealed record HeutigesDatum(string Datum, string Wochentag, string Uhrzeit, string Zeitzone);
}
