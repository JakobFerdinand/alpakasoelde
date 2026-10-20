using System.Diagnostics;
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
/// fixed tool anticipated with a single SELECT. What may enter the database is defined once per table in
/// <see cref="AssistantTables"/>: buyer names, contact details, message texts and image links never get in,
/// so no query can reach them. Writes are refused twice over — by <see cref="Validate"/> and by an authorizer
/// that only admits reads — and a runaway query is interrupted after <see cref="Timeout"/>.
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

	private static void CreateSchema(SqliteConnection connection) => Execute(connection, AssistantTables.CreateSchemaSql);

	private async Task LoadAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		// The only per-request part: binding the static table shapes to this request's stores.
		AssistantSources sources = new(pageViews, messages, gutscheine, alpakas, events);

		using SqliteTransaction transaction = connection.BeginTransaction();

		foreach (IAssistantTable table in AssistantTables.All)
		{
			foreach (IReadOnlyList<object?> row in await table.ReadAsync(sources, cancellationToken).ConfigureAwait(false))
			{
				Insert(connection, table.Name, [.. row]);
			}
		}

		transaction.Commit();
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
