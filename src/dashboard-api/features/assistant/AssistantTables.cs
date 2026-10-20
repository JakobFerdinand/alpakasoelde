using System.Globalization;
using DashboardApi.Features.Alpakas;
using DashboardApi.Features.Gutscheine;
using DashboardApi.Features.Messages;
using DashboardApi.Features.PageViews;
using dashboard_api.shared.entities;
using EventsFeature = DashboardApi.Features.Events.Events;

namespace DashboardApi.Features.Assistant;

/// <summary>
/// Every assistant table, defined exactly once: its allowed columns — SQLite type, hand-written schema
/// annotation and pure value function — the load that pre-projects its source store data into its rows
/// (row filters, the Vienna-timezone columns, path normalisation, referrer rules and the event-to-alpaka-name
/// join) and the tool-record projection that hands the model its result while dropping the private value
/// (<c>VerkauftAn</c>, <c>ImageUrl</c>). Whatever the value and projection functions never return — buyer
/// names, contact details, message texts and image links — never reaches the model, so this file is the
/// whole privacy rule of <c>daten_abfrage</c> and the assistant tools, and the CREATE TABLE DDL, the
/// INSERTs and the model-facing schema text are all generated from it: a column added in one definition
/// shows up everywhere or nowhere.
/// </summary>
public static class AssistantTables
{
	/// <summary>Every assistant table in DDL, INSERT and schema-text order.</summary>
	public static IReadOnlyList<IAssistantTable> All =>
		[Seitenaufrufe.Tabelle, Nachrichten.Tabelle, Gutscheine.Tabelle, Alpakas.Tabelle, Ereignisse.Tabelle];

	/// <summary>The generated CREATE TABLE statements of all tables.</summary>
	public static string CreateSchemaSql => string.Join('\n', All.Select(table => table.CreateSql));

	/// <summary>
	/// The schema text as the model sees it: the hand-written header and the hand-written per-table intro
	/// lines, with only the column lists generated from the definitions.
	/// </summary>
	public static string SchemaText => string.Join('\n',
		[
			"Alle Zeitangaben sind in der Zeitzone Europe/Vienna. Textspalten wie datum ('JJJJ-MM-TT') und zeitpunkt",
			"('JJJJ-MM-TT HH:MM:SS') funktionieren mit den SQLite-Datumsfunktionen (date, strftime, julianday).",
			"",
			.. All.Select(table => table.SchemaLines),
		]);

	/// <summary>Converts a moment into the Vienna-timezone columns shared by three of the tables.</summary>
	private static (string Zeitpunkt, string Datum, string Monat, string Wochentag, int WochentagNr, int Stunde) Moment(DateTimeOffset value)
	{
		DateTimeOffset local = TimeZoneInfo.ConvertTime(value, AssistantTools.ViennaTimeZone);
		int number = local.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;
		return (
			local.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
			local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			local.ToString("yyyy-MM", CultureInfo.InvariantCulture),
			AssistantTools.GermanWeekday(local.DayOfWeek),
			number,
			local.Hour);
	}

	private static string GetPathOf(string path)
	{
		string trimmed = path.Trim().TrimEnd('/');
		return trimmed.Length == 0 ? "/" : trimmed;
	}

	private static class Seitenaufrufe
	{
		internal static readonly IAssistantTable Tabelle = new AssistantTable<Zeile>(
			"seitenaufrufe",
			"ein Seitenaufruf der Website, nur die letzten 180 Tage",
			[
				Col("zeitpunkt", "TEXT", null, row => row.Zeitpunkt),
				Col("datum", "TEXT", null, row => row.Datum),
				Col("monat", "TEXT", "'JJJJ-MM'", row => row.Monat),
				Col("wochentag", "TEXT", "'Montag' bis 'Sonntag'", row => row.Wochentag),
				Col("wochentag_nr", "INTEGER", "1 = Montag bis 7 = Sonntag", row => row.WochentagNr),
				Col("stunde", "INTEGER", "0-23", row => row.Stunde),
				Col("pfad", "TEXT", null, row => row.Pfad),
				Col("herkunft", "TEXT", "externe Herkunftsseite oder NULL", row => row.Herkunft),
				Col("geraet", "TEXT", "'Mobil', 'Tablet', 'Laptop', 'Breitbild'", row => row.Geraet),
				Col("navigationsart", "TEXT", null, row => row.Navigationsart),
				Col("sitzung_id", "TEXT", null, row => row.SitzungId),
				Col("besucher_id", "TEXT", null, row => row.BesucherId),
			],
			LoadAsync);

		private static async Task<IReadOnlyList<Zeile>> LoadAsync(AssistantSources sources, CancellationToken cancellationToken)
		{
			List<Zeile> rows = [];
			foreach (PageViewEntity view in await sources.PageViews.GetAllAsync(cancellationToken).ConfigureAwait(false))
			{
				if (!view.Timestamp.HasValue)
				{
					continue;
				}

				var (zeitpunkt, datum, monat, wochentag, nr, stunde) = Moment(view.Timestamp.Value);
				string? origin = string.IsNullOrWhiteSpace(view.ReferrerHost) || GetPageViewStats.Handler.IsInternalReferrer(view.ReferrerHost)
					? null
					: view.ReferrerHost.Trim().ToLowerInvariant();

				rows.Add(new Zeile(zeitpunkt, datum, monat, wochentag, nr, stunde, GetPathOf(view.Path), origin,
					GetPageViewStats.Handler.GetDeviceCategory(view.ViewportWidth), view.NavigationType, view.SessionId, view.VisitorId));
			}

			return rows;
		}

		private sealed record Zeile(
			string Zeitpunkt, string Datum, string Monat, string Wochentag, int WochentagNr, int Stunde,
			string Pfad, string? Herkunft, string Geraet, string? Navigationsart, string? SitzungId, string? BesucherId);

		private static AssistantColumn<Zeile> Col(string name, string sqlType, string? hinweis, Func<Zeile, object?> value) =>
			new(name, sqlType, hinweis, value);
	}

	private static class Nachrichten
	{
		internal static readonly IAssistantTable Tabelle = new AssistantTable<Zeile>(
			"nachrichten",
			"eine Kontaktanfrage, ohne Namen, Kontaktdaten und Text",
			[
				Col("zeitpunkt", "TEXT", null, row => row.Zeitpunkt),
				Col("datum", "TEXT", null, row => row.Datum),
				Col("monat", "TEXT", null, row => row.Monat),
				Col("wochentag", "TEXT", null, row => row.Wochentag),
				Col("wochentag_nr", "INTEGER", null, row => row.WochentagNr),
				Col("stunde", "INTEGER", null, row => row.Stunde),
				Col("ist_spam", "INTEGER", "1 = Spam, 0 = echt", row => row.IstSpam),
			],
			LoadAsync);

		private static async Task<IReadOnlyList<Zeile>> LoadAsync(AssistantSources sources, CancellationToken cancellationToken)
		{
			List<Zeile> rows = [];
			foreach (MessageEntity message in await sources.Messages.GetAllAsync(cancellationToken).ConfigureAwait(false))
			{
				if (!message.Timestamp.HasValue)
				{
					continue;
				}

				var (zeitpunkt, datum, monat, wochentag, nr, stunde) = Moment(message.Timestamp.Value);

				// Name, Email, Phone and Message stay behind and never reach even this row record.
				rows.Add(new Zeile(zeitpunkt, datum, monat, wochentag, nr, stunde, message.IsSpam ? 1 : 0));
			}

			return rows;
		}

		private sealed record Zeile(
			string Zeitpunkt, string Datum, string Monat, string Wochentag, int WochentagNr, int Stunde, int IstSpam);

		private static AssistantColumn<Zeile> Col(string name, string sqlType, string? hinweis, Func<Zeile, object?> value) =>
			new(name, sqlType, hinweis, value);
	}

	internal static class Gutscheine
	{
		internal static readonly IAssistantTable Tabelle = new AssistantTable<Zeile>(
			"gutscheine",
			"ein Gutschein, ohne Käufer",
			[
				Col("gutscheinnummer", "TEXT", null, row => row.Gutscheinnummer),
				Col("kaufdatum", "TEXT", null, row => row.Kaufdatum),
				Col("betrag", "REAL", null, row => row.Betrag),
				Col("eingeloest_am", "TEXT", "oder NULL", row => row.EingeloestAm),
				Col("offen", "INTEGER", "1 = noch nicht eingelöst", row => row.Offen),
			],
			LoadAsync);

		private static async Task<IReadOnlyList<Zeile>> LoadAsync(AssistantSources sources, CancellationToken cancellationToken)
		{
			List<Zeile> rows = [];
			foreach (GutscheinEntity voucher in (await sources.Gutscheine.GetAllAsync(cancellationToken).ConfigureAwait(false))
				.OrderByDescending(voucher => voucher.Kaufdatum)
				.ThenByDescending(voucher => voucher.Gutscheinnummer))
			{
				// VerkauftAn is dropped here and never enters the database.
				rows.Add(new Zeile(voucher.Gutscheinnummer, voucher.Kaufdatum.ToString("yyyy-MM-dd"), voucher.Betrag,
					voucher.EingeloestAm?.ToString("yyyy-MM-dd"), voucher.EingeloestAm is null ? 1 : 0));
			}

			return rows;
		}

		private sealed record Zeile(string Gutscheinnummer, string Kaufdatum, double Betrag, string? EingeloestAm, int Offen);

		/// <summary>
		/// Projects the store's voucher results into the tool's list record. Projected here rather than
		/// filtered later: VerkauftAn must never reach the model.
		/// </summary>
		internal static AssistantTools.GutscheinListe Liste(IReadOnlyList<GetGutscheine.GutscheinResult> vouchers, bool nurOffen)
		{
			List<AssistantTools.GutscheinInfo> rows = vouchers
				.Where(voucher => !nurOffen || voucher.EingeloestAm is null)
				.Select(voucher => new AssistantTools.GutscheinInfo(
					voucher.Gutscheinnummer,
					voucher.Kaufdatum,
					voucher.Betrag,
					voucher.EingeloestAm,
					voucher.EingeloestAm is null))
				.ToList();

			var (kept, hinweis) = AssistantTools.Clamp(rows, AssistantTools.MaxRows);
			int openCount = rows.Count(voucher => voucher.Offen);
			double openSum = rows.Where(voucher => voucher.Offen).Sum(voucher => voucher.Betrag);
			return new(rows.Count, openCount, openSum, kept, hinweis);
		}

		private static AssistantColumn<Zeile> Col(string name, string sqlType, string? hinweis, Func<Zeile, object?> value) =>
			new(name, sqlType, hinweis, value);
	}

	internal static class Alpakas
	{
		internal static readonly IAssistantTable Tabelle = new AssistantTable<Zeile>(
			"alpakas",
			"ein Alpaka",
			[
				Col("id", "TEXT", null, row => row.Id),
				Col("name", "TEXT", null, row => row.Name),
				Col("geburtsdatum", "TEXT", null, row => row.Geburtsdatum),
			],
			LoadAsync);

		private static async Task<IReadOnlyList<Zeile>> LoadAsync(AssistantSources sources, CancellationToken cancellationToken)
		{
			List<Zeile> rows = [];
			foreach (AlpakaEntity animal in await sources.Alpakas.GetAllAsync(cancellationToken).ConfigureAwait(false))
			{
				// ImageUrl is a signed link and stays out.
				rows.Add(new Zeile(animal.RowKey, animal.Name, animal.Geburtsdatum));
			}

			return rows;
		}

		private sealed record Zeile(string Id, string Name, string Geburtsdatum);

		/// <summary>
		/// Projects the store's alpaka list into the tool's list record. ImageUrl is a short-lived signed SAS
		/// link: useless to the model and not something to hand out.
		/// </summary>
		internal static AssistantTools.AlpakaListe Liste(IReadOnlyList<GetAlpakas.AlpakaListItem> alpakas)
		{
			List<AssistantTools.AlpakaInfo> rows = alpakas
				.Select(alpaka => new AssistantTools.AlpakaInfo(alpaka.Id, alpaka.Name, alpaka.Geburtsdatum))
				.ToList();

			var (kept, hinweis) = AssistantTools.Clamp(rows, AssistantTools.MaxRows);
			return new(rows.Count, kept, hinweis);
		}

		private static AssistantColumn<Zeile> Col(string name, string sqlType, string? hinweis, Func<Zeile, object?> value) =>
			new(name, sqlType, hinweis, value);
	}

	private static class Ereignisse
	{
		internal static readonly IAssistantTable Tabelle = new AssistantTable<Zeile>(
			"ereignisse",
			"ein Ereignis eines Alpakas (ein gemeinsames Ereignis mehrerer Alpakas ergibt mehrere Zeilen)",
			[
				Col("ereignis_id", "TEXT", null, row => row.EreignisId),
				Col("alpaka_id", "TEXT", null, row => row.AlpakaId),
				Col("alpaka_name", "TEXT", null, row => row.AlpakaName),
				Col("typ", "TEXT", null, row => row.Typ),
				Col("datum", "TEXT", null, row => row.Datum),
				Col("monat", "TEXT", null, row => row.Monat),
				Col("kommentar", "TEXT", null, row => row.Kommentar),
				Col("kosten", "REAL", "oder NULL", row => row.Kosten),
			],
			LoadAsync);

		private static async Task<IReadOnlyList<Zeile>> LoadAsync(AssistantSources sources, CancellationToken cancellationToken)
		{
			Dictionary<string, string> names = [];
			foreach (AlpakaEntity animal in await sources.Alpakas.GetAllAsync(cancellationToken).ConfigureAwait(false))
			{
				names[animal.RowKey] = animal.Name;
			}

			List<Zeile> rows = [];
			foreach (EventEntity entry in await sources.Events.GetAllAsync(cancellationToken).ConfigureAwait(false))
			{
				// SharedEventId stays behind: ereignis_id is the per-row key, a shared event appears once per alpaka.
				var (_, datum, monat, _, _, _) = Moment(entry.EventDate);
				rows.Add(new Zeile(entry.RowKey, entry.PartitionKey, names.GetValueOrDefault(entry.PartitionKey),
					entry.EventType, datum, monat, entry.Comment, entry.Cost));
			}

			return rows;
		}

		private sealed record Zeile(
			string EreignisId, string AlpakaId, string? AlpakaName, string Typ, string Datum, string Monat,
			string? Kommentar, double? Kosten);

		private static AssistantColumn<Zeile> Col(string name, string sqlType, string? hinweis, Func<Zeile, object?> value) =>
			new(name, sqlType, hinweis, value);
	}
}

/// <summary>The per-request binding of the tables to their five store interfaces.</summary>
public sealed record AssistantSources(
	GetPageViewStats.IPageViewReadStore PageViews,
	GetMessages.IReadStore Messages,
	IGutscheinStore Gutscheine,
	GetAlpakas.IAlpakaReadStore Alpakas,
	EventsFeature.IEventStore Events);

/// <summary>One allowed column of a table: its SQLite type in the generated DDL, its hand-written annotation in
/// the generated schema text, and the pure value function from the table's row to the inserted value.</summary>
public sealed record AssistantColumn<TRow>(string Name, string SqlType, string? Hinweis, Func<TRow, object?> Value);

/// <summary>What the query engine and the schema text need from a table definition.</summary>
public interface IAssistantTable
{
	/// <summary>The table name, identical in the DDL, the INSERTs and the schema text.</summary>
	string Name { get; }

	/// <summary>The hand-written German one-liner that introduces the table in the schema text.</summary>
	string Beschreibung { get; }

	/// <summary>The generated CREATE TABLE statement.</summary>
	string CreateSql { get; }

	/// <summary>The schema-text section: the intro line, then the generated column list.</summary>
	string SchemaLines { get; }

	/// <summary>Projects the bound store data and returns each row's insert values, in column order.</summary>
	Task<IReadOnlyList<IReadOnlyList<object?>>> ReadAsync(AssistantSources sources, CancellationToken cancellationToken);
}

/// <summary>
/// One assistant table: its name, its hand-written German intro, its allowed columns, and the load that
/// pre-projects the store data into its rows. The shape is static and pure; only <see cref="ReadAsync"/>
/// touches state, and only the stores bound per request through <see cref="AssistantSources"/>.
/// </summary>
public sealed record AssistantTable<TRow>(
	string Name,
	string Beschreibung,
	IReadOnlyList<AssistantColumn<TRow>> Columns,
	Func<AssistantSources, CancellationToken, Task<IReadOnlyList<TRow>>> Load) : IAssistantTable
{
	/// <summary>Line width the hand-written schema text was wrapped at.</summary>
	private const int MaxSchemaLineLength = 120;

	public string CreateSql =>
		$"CREATE TABLE {Name} ({string.Join(", ", Columns.Select(column => $"{column.Name} {column.SqlType}"))});";

	public string SchemaLines => $"{Name} — {Beschreibung}\n{string.Join('\n', WrappedColumnLines())}";

	public async Task<IReadOnlyList<IReadOnlyList<object?>>> ReadAsync(AssistantSources sources, CancellationToken cancellationToken)
	{
		IReadOnlyList<TRow> rows = await Load(sources, cancellationToken).ConfigureAwait(false);
		return [.. rows.Select(row => (IReadOnlyList<object?>)[.. Columns.Select(column => column.Value(row))])];
	}

	/// <summary>Wraps the generated column entries like the hand-written text did, never splitting one.</summary>
	private IEnumerable<string> WrappedColumnLines()
	{
		List<string> lines = [];
		List<string> entries = [];

		foreach (string entry in Columns.Select(column => column.Hinweis is null ? column.Name : $"{column.Name} ({column.Hinweis})"))
		{
			if (entries.Count > 0 && 2 + string.Join(", ", [.. entries, entry]).Length + 1 > MaxSchemaLineLength)
			{
				lines.Add($"  {string.Join(", ", entries)},");
				entries = [entry];
			}
			else
			{
				entries.Add(entry);
			}
		}

		lines.Add($"  {string.Join(", ", entries)}");
		return lines;
	}
}
