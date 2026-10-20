using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;
using DashboardApi.Features.Alpakas;
using DashboardApi.Features.Gutscheine;
using DashboardApi.Features.Messages;
using DashboardApi.Features.PageViews;
using SQLitePCL;
using EventsFeature = DashboardApi.Features.Events.Events;

namespace DashboardApi.Features.Assistant;

/// <summary>
/// A throwaway in-memory SQLite copy of the data the assistant may see, so the model can answer questions no
/// fixed tool anticipated with a single SELECT. Privacy is enforced by what is loaded: buyer names, contact
/// details, message texts and image links never enter the database, so no query can reach them. Writes are
/// refused twice over — by <see cref="Validate"/> and by an authorizer that only admits reads — and a runaway
/// query is interrupted after <see cref="Timeout"/>.
/// </summary>
public sealed class AssistantDatabase(
	GetPageViewStats.IPageViewReadStore pageViews,
	GetMessages.IReadStore messages,
	IGutscheinStore gutscheine,
	GetAlpakas.IAlpakaReadStore alpakas,
	EventsFeature.IEventStore events)
{
	public const int MaxSqlLength = 2000;

	/// <summary>Rows read from a query before it is cut off; the tool clamps further to what the model gets.</summary>
	public const int MaxRows = AssistantTools.MaxRows + 1;

	public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

	/// <summary>The schema as the model sees it; it is part of the tool description.</summary>
	public const string Schema = """
		Alle Zeitangaben sind in der Zeitzone Europe/Vienna. Textspalten wie datum ('JJJJ-MM-TT') und zeitpunkt
		('JJJJ-MM-TT HH:MM:SS') funktionieren mit den SQLite-Datumsfunktionen (date, strftime, julianday).

		seitenaufrufe — ein Seitenaufruf der Website, nur die letzten 180 Tage
		  zeitpunkt, datum, monat ('JJJJ-MM'), wochentag ('Montag' bis 'Sonntag'), wochentag_nr (1 = Montag bis 7 = Sonntag),
		  stunde (0-23), pfad, herkunft (externe Herkunftsseite oder NULL), geraet ('Mobil', 'Tablet', 'Laptop', 'Breitbild'),
		  navigationsart, sitzung_id, besucher_id
		nachrichten — eine Kontaktanfrage, ohne Namen, Kontaktdaten und Text
		  zeitpunkt, datum, monat, wochentag, wochentag_nr, stunde, ist_spam (1 = Spam, 0 = echt)
		gutscheine — ein Gutschein, ohne Käufer
		  gutscheinnummer, kaufdatum, betrag, eingeloest_am (oder NULL), offen (1 = noch nicht eingelöst)
		alpakas — ein Alpaka
		  id, name, geburtsdatum
		ereignisse — ein Ereignis eines Alpakas (ein gemeinsames Ereignis mehrerer Alpakas ergibt mehrere Zeilen)
		  ereignis_id, alpaka_id, alpaka_name, typ, datum, monat, kommentar, kosten (oder NULL)
		""";

	public sealed record QueryResult(
		IReadOnlyList<string> Columns,
		IReadOnlyList<IReadOnlyList<object?>> Rows,
		string? Error = null)
	{
		public static QueryResult Failed(string error) => new([], [], error);
	}

	/// <summary>Loads the data and runs <paramref name="sql"/>; a rejected or failing query comes back as an error text.</summary>
	public async Task<QueryResult> QueryAsync(string sql, CancellationToken cancellationToken)
	{
		string? problem = Validate(sql, out string statement);
		if (problem is not null)
		{
			return QueryResult.Failed(problem);
		}

		using SqliteConnection connection = new("Data Source=:memory:");
		connection.Open();

		CreateSchema(connection);
		await LoadAsync(connection, cancellationToken).ConfigureAwait(false);
		Lock(connection);

		try
		{
			using SqliteCommand command = connection.CreateCommand();
			command.CommandText = statement;

			using SqliteDataReader reader = command.ExecuteReader();
			string[] columns = [.. Enumerable.Range(0, reader.FieldCount).Select(reader.GetName)];
			List<IReadOnlyList<object?>> rows = [];

			while (rows.Count < MaxRows && reader.Read())
			{
				object?[] row = new object?[reader.FieldCount];
				for (int i = 0; i < row.Length; i++)
				{
					row[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
				}

				rows.Add(row);
			}

			return new QueryResult(columns, rows);
		}
		catch (SqliteException exception)
		{
			// The message names the unknown column or the syntax slip, which is what lets the model correct itself.
			return QueryResult.Failed(exception.Message);
		}
	}

	/// <summary>Admits exactly one SELECT (optionally behind WITH); everything else is refused before it reaches SQLite.</summary>
	internal static string? Validate(string? sql, out string statement)
	{
		statement = (sql ?? string.Empty).Trim().TrimEnd(';').Trim();

		if (statement.Length == 0)
		{
			return "Die Abfrage ist leer.";
		}

		if (statement.Length > MaxSqlLength)
		{
			return $"Die Abfrage ist länger als {MaxSqlLength} Zeichen.";
		}

		if (!statement.StartsWith("select", StringComparison.OrdinalIgnoreCase)
			&& !statement.StartsWith("with", StringComparison.OrdinalIgnoreCase))
		{
			return "Erlaubt ist nur eine einzelne SELECT-Abfrage.";
		}

		if (ContainsStatementSeparator(statement))
		{
			return "Erlaubt ist nur eine einzelne SELECT-Abfrage.";
		}

		return null;
	}

	private static bool ContainsStatementSeparator(string statement)
	{
		char? quote = null;
		foreach (char c in statement)
		{
			if (quote is not null)
			{
				if (c == quote)
				{
					quote = null;
				}
			}
			else if (c is '\'' or '"' or '`')
			{
				quote = c;
			}
			else if (c == ';')
			{
				return true;
			}
		}

		return false;
	}

	private static void CreateSchema(SqliteConnection connection) => Execute(connection, """
		CREATE TABLE seitenaufrufe (zeitpunkt TEXT, datum TEXT, monat TEXT, wochentag TEXT, wochentag_nr INTEGER, stunde INTEGER,
			pfad TEXT, herkunft TEXT, geraet TEXT, navigationsart TEXT, sitzung_id TEXT, besucher_id TEXT);
		CREATE TABLE nachrichten (zeitpunkt TEXT, datum TEXT, monat TEXT, wochentag TEXT, wochentag_nr INTEGER, stunde INTEGER, ist_spam INTEGER);
		CREATE TABLE gutscheine (gutscheinnummer TEXT, kaufdatum TEXT, betrag REAL, eingeloest_am TEXT, offen INTEGER);
		CREATE TABLE alpakas (id TEXT, name TEXT, geburtsdatum TEXT);
		CREATE TABLE ereignisse (ereignis_id TEXT, alpaka_id TEXT, alpaka_name TEXT, typ TEXT, datum TEXT, monat TEXT, kommentar TEXT, kosten REAL);
		""");

	private async Task LoadAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		var views = await pageViews.GetAllAsync(cancellationToken).ConfigureAwait(false);
		var messageRows = await messages.GetAllAsync(cancellationToken).ConfigureAwait(false);
		var vouchers = (await gutscheine.GetAllAsync(cancellationToken).ConfigureAwait(false))
			.OrderByDescending(voucher => voucher.Kaufdatum)
			.ThenByDescending(voucher => voucher.Gutscheinnummer);
		var animals = await alpakas.GetAllAsync(cancellationToken).ConfigureAwait(false);
		var eventRows = await events.GetAllAsync(cancellationToken).ConfigureAwait(false);

		using SqliteTransaction transaction = connection.BeginTransaction();

		foreach (var view in views.Where(v => v.Timestamp.HasValue))
		{
			var (zeitpunkt, datum, monat, wochentag, nr, stunde) = Moment(view.Timestamp!.Value);
			string? origin = string.IsNullOrWhiteSpace(view.ReferrerHost) || GetPageViewStats.Handler.IsInternalReferrer(view.ReferrerHost)
				? null
				: view.ReferrerHost.Trim().ToLowerInvariant();

			Insert(connection, "seitenaufrufe", zeitpunkt, datum, monat, wochentag, nr, stunde,
				GetPathOf(view.Path), origin, GetPageViewStats.Handler.GetDeviceCategory(view.ViewportWidth),
				view.NavigationType, view.SessionId, view.VisitorId);
		}

		foreach (var message in messageRows.Where(m => m.Timestamp.HasValue))
		{
			var (zeitpunkt, datum, monat, wochentag, nr, stunde) = Moment(message.Timestamp!.Value);
			Insert(connection, "nachrichten", zeitpunkt, datum, monat, wochentag, nr, stunde, message.IsSpam ? 1 : 0);
		}

		foreach (var voucher in vouchers)
		{
			// VerkauftAn is dropped here and never enters the database.
			Insert(connection, "gutscheine", voucher.Gutscheinnummer, voucher.Kaufdatum.ToString("yyyy-MM-dd"), voucher.Betrag,
				voucher.EingeloestAm?.ToString("yyyy-MM-dd"), voucher.EingeloestAm is null ? 1 : 0);
		}

		Dictionary<string, string> names = [];
		foreach (var animal in animals)
		{
			// ImageUrl is a signed link and stays out.
			names[animal.RowKey] = animal.Name;
			Insert(connection, "alpakas", animal.RowKey, animal.Name, animal.Geburtsdatum);
		}

		foreach (var entry in eventRows)
		{
			var (_, datum, monat, _, _, _) = Moment(entry.EventDate);
			Insert(connection, "ereignisse", entry.RowKey, entry.PartitionKey, names.GetValueOrDefault(entry.PartitionKey),
				entry.EventType, datum, monat, entry.Comment, entry.Cost);
		}

		transaction.Commit();
	}

	private static string GetPathOf(string path)
	{
		string trimmed = path.Trim().TrimEnd('/');
		return trimmed.Length == 0 ? "/" : trimmed;
	}

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

	private static void Insert(SqliteConnection connection, string table, params object?[] values)
	{
		using SqliteCommand command = connection.CreateCommand();
		command.CommandText = $"INSERT INTO {table} VALUES ({string.Join(',', values.Select((_, i) => $"$p{i}"))})";
		for (int i = 0; i < values.Length; i++)
		{
			command.Parameters.AddWithValue($"$p{i}", values[i] ?? DBNull.Value);
		}

		command.ExecuteNonQuery();
	}

	private static void Execute(SqliteConnection connection, string sql)
	{
		using SqliteCommand command = connection.CreateCommand();
		command.CommandText = sql;
		command.ExecuteNonQuery();
	}

	/// <summary>Switches the connection to read-only and arms the deadline, once the data is in.</summary>
	private static void Lock(SqliteConnection connection)
	{
		Execute(connection, "PRAGMA query_only = ON");

		sqlite3 handle = connection.Handle!;

		// Anything but reading is denied at prepare time, whatever the statement turns out to be.
		raw.sqlite3_set_authorizer(handle, (strdelegate_authorizer)((_, action, _, _, _, _) =>
			action is raw.SQLITE_SELECT or raw.SQLITE_READ or raw.SQLITE_FUNCTION or raw.SQLITE_RECURSIVE
				? raw.SQLITE_OK
				: raw.SQLITE_DENY), null);

		Stopwatch clock = Stopwatch.StartNew();
		raw.sqlite3_progress_handler(handle, 1000, _ => clock.Elapsed > Timeout ? 1 : 0, null);
	}
}
