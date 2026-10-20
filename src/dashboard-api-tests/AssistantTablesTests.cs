using System.Text.RegularExpressions;
using DashboardApi.Features.Assistant;
using DashboardApi.Tests.Fakes;
using Microsoft.Extensions.AI;

namespace DashboardApi.Tests;

public sealed class AssistantTablesTests
{
	private static CancellationToken Ct => TestContext.Current.CancellationToken;

	[Fact]
	public void The_schema_text_keeps_its_hand_written_text()
	{
		Assert.Equal(OldSchemaText, AssistantTables.SchemaText);
	}

	[Fact]
	public void The_sql_tool_carries_the_generated_schema_text_as_its_registered_description()
	{
		AIFunction sql = new AssistantFixture().BuildTools().All
			.OfType<AIFunction>()
			.Single(function => function.Name == "daten_abfrage");

		Assert.Equal(OldDescriptionProse + "\n\nTabellen:\n" + AssistantTables.SchemaText, sql.Description);
	}

	[Theory]
	[InlineData("seitenaufrufe")]
	[InlineData("nachrichten")]
	[InlineData("gutscheine")]
	[InlineData("alpakas")]
	[InlineData("ereignisse")]
	public async Task Selecting_a_table_returns_exactly_the_columns_its_schema_text_promises(string table)
	{
		AssistantFixture fixture = new();
		SeedOneRow(fixture, table);

		AssistantTools.DatenAbfrage result = await fixture.BuildTools().DatenAbfrageAsync($"SELECT * FROM {table}", Ct);

		Assert.Null(result.Fehler);
		Assert.Equal(ColumnsInSchemaText(AssistantTables.SchemaText, table), result.Spalten);
	}

	private static void SeedOneRow(AssistantFixture fixture, string table)
	{
		switch (table)
		{
			case "seitenaufrufe":
				fixture.PageViews.Add(InMemoryPageViewReadStore.View(DateTimeOffset.UtcNow, "/wandern/", "v-1"));
				break;
			case "nachrichten":
				fixture.Messages.Add(AssistantFixture.Message(daysAgo: 1));
				break;
			case "gutscheine":
				fixture.Gutscheine.Add(InMemoryGutscheinStore.Gutschein("202501", DateTimeOffset.UtcNow));
				break;
			case "alpakas":
				fixture.Alpakas.Add(AssistantFixture.Alpaka("alpaka-1", "Richard"));
				break;
			case "ereignisse":
				fixture.Alpakas.Add(AssistantFixture.Alpaka("alpaka-1", "Richard"));
				fixture.Events.Add(AssistantFixture.Event(
					"alpaka-1", "e-1", "Scheren", new DateTimeOffset(2026, 5, 10, 0, 0, 0, TimeSpan.Zero), cost: 30));
				break;
		}
	}

	/// <summary>Reads the column names of a table's section out of the generated schema text.</summary>
	private static IReadOnlyList<string> ColumnsInSchemaText(string schema, string table)
	{
		string[] lines = schema.Split('\n');
		int intro = Array.FindIndex(lines, line => line.StartsWith($"{table} —", StringComparison.Ordinal));
		List<string> indented = [];
		for (int i = intro + 1; i < lines.Length && lines[i].StartsWith("  ", StringComparison.Ordinal); i++)
		{
			indented.Add(lines[i][2..]);
		}

		// Value hints in parentheses can contain commas of their own, so they go before the split.
		string withoutHints = Regex.Replace(string.Join(' ', indented), @"\([^)]*\)", "");
		return [.. withoutHints.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
	}

	private const string OldSchemaText = """
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

	private const string OldDescriptionProse = """
		Führt eine einzelne schreibgeschützte SQL-Abfrage (SQLite, nur SELECT) gegen die Daten der Farm aus und
		liefert Spalten und Zeilen. Nutze dieses Werkzeug für alle Auswertungen, die die anderen Werkzeuge nicht
		abdecken, etwa Aufschlüsselungen nach Wochentag, Uhrzeit oder Monat, Vergleiche, Quoten und Rangfolgen.
		Zähle und summiere mit GROUP BY in der Abfrage selbst, statt viele Zeilen zu holen. Es kommen nur die ersten Zeilen
		zurück, bei „gekürzt" fehlen weitere. Bei einem Fehler steht er im Feld 'Fehler', korrigiere dann die Abfrage.
		""";
}
