using DashboardApi.Features.Events;
using dashboard_api.shared.entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashboardApi.Tests;

public sealed class EventsHandlerTests
{
	private sealed class RecordingEventStore(params EventEntity[] seed) : Events.IEventStore
	{
		public List<EventEntity> Entities { get; } = [.. seed];

		public Task<IReadOnlyList<EventEntity>> GetAllAsync(CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<EventEntity>>(Entities);

		public Task AddAsync(EventEntity entity, CancellationToken cancellationToken)
		{
			Entities.Add(entity);
			return Task.CompletedTask;
		}
	}

	private sealed class StubAlpakaLookup(Dictionary<string, string> names) : Events.IAlpakaLookupStore
	{
		public Task<IDictionary<string, string>> GetNamesAsync(CancellationToken cancellationToken)
			=> Task.FromResult<IDictionary<string, string>>(names);
	}

	private static EventEntity Row(string alpakaId, string rowKey, string sharedEventId, string eventType, DateTimeOffset eventDate) => new()
	{
		EventType = eventType,
		EventDate = eventDate,
		PartitionKey = alpakaId,
		RowKey = rowKey,
		SharedEventId = sharedEventId
	};

	[Fact]
	public async Task One_event_over_several_alpakas_becomes_one_row_per_alpaka_sharing_an_id()
	{
		RecordingEventStore store = new();
		Events.AddHandler handler = new(store, NullLogger<Events.AddHandler>.Instance);

		var (result, error) = await handler.HandleAsync(
			new Events.AddCommand("  Scheren  ", ["alpaka-1", "alpaka-2", "alpaka-3"], "2025-05-02T09:15:00", 180, "  Ganze Herde  "),
			TestContext.Current.CancellationToken);

		Assert.Null(error);
		Assert.NotNull(result);
		Assert.Equal(3, store.Entities.Count);
		Assert.Equal(["alpaka-1", "alpaka-2", "alpaka-3"], store.Entities.Select(e => e.PartitionKey));

		// The shared id is what groups the rows back together on read, and it doubles
		// as the RowKey so one alpaka cannot hold the same event twice.
		Assert.All(store.Entities, entity =>
		{
			Assert.Equal(result.Id, entity.SharedEventId);
			Assert.Equal(result.Id, entity.RowKey);
			Assert.Equal("Scheren", entity.EventType);
			Assert.Equal("Ganze Herde", entity.Comment);
			Assert.Equal(180, entity.Cost);
			Assert.Equal(new DateTimeOffset(2025, 5, 2, 0, 0, 0, TimeSpan.Zero), entity.EventDate);
		});
	}

	[Theory]
	[InlineData("", "2025-05-02", "Das Ereignisfeld ist erforderlich.")]
	[InlineData("Scheren", "irgendwann", "Das Datum ist ungültig.")]
	public async Task Rejected_event_reports_the_problem_and_writes_nothing(string eventType, string eventDate, string expectedError)
	{
		RecordingEventStore store = new();
		Events.AddHandler handler = new(store, NullLogger<Events.AddHandler>.Instance);

		var (result, error) = await handler.HandleAsync(
			new Events.AddCommand(eventType, ["alpaka-1"], eventDate, null, null),
			TestContext.Current.CancellationToken);

		Assert.Null(result);
		Assert.Equal(expectedError, error);
		Assert.Empty(store.Entities);
	}

	[Fact]
	public async Task An_event_without_alpakas_is_rejected()
	{
		RecordingEventStore store = new();
		Events.AddHandler handler = new(store, NullLogger<Events.AddHandler>.Instance);

		var (result, error) = await handler.HandleAsync(
			new Events.AddCommand("Scheren", [], "2025-05-02", null, null),
			TestContext.Current.CancellationToken);

		Assert.Null(result);
		Assert.Equal("Mindestens ein Alpaka muss ausgewählt werden.", error);
		Assert.Empty(store.Entities);
	}

	[Fact]
	public async Task Negative_costs_and_oversized_text_are_rejected()
	{
		RecordingEventStore store = new();
		Events.AddHandler handler = new(store, NullLogger<Events.AddHandler>.Instance);

		var (_, negativeCost) = await handler.HandleAsync(
			new Events.AddCommand("Scheren", ["alpaka-1"], "2025-05-02", -1, null),
			TestContext.Current.CancellationToken);
		var (_, longType) = await handler.HandleAsync(
			new Events.AddCommand(new string('x', 101), ["alpaka-1"], "2025-05-02", null, null),
			TestContext.Current.CancellationToken);
		var (_, longComment) = await handler.HandleAsync(
			new Events.AddCommand("Scheren", ["alpaka-1"], "2025-05-02", null, new string('x', 1001)),
			TestContext.Current.CancellationToken);

		Assert.Equal("Kosten dürfen nicht negativ sein.", negativeCost);
		Assert.Equal("Der Ereignistyp darf maximal 100 Zeichen lang sein.", longType);
		Assert.Equal("Die Notiz darf maximal 1000 Zeichen enthalten.", longComment);
		Assert.Empty(store.Entities);
	}

	[Fact]
	public async Task Reading_groups_the_shared_rows_and_resolves_the_alpaka_names()
	{
		RecordingEventStore store = new(
			Row("alpaka-1", "shared-1", "shared-1", "Scheren", new DateTimeOffset(2025, 5, 2, 0, 0, 0, TimeSpan.Zero)),
			Row("alpaka-2", "shared-1", "shared-1", "Scheren", new DateTimeOffset(2025, 5, 2, 0, 0, 0, TimeSpan.Zero)),
			// An alpaka that has since been deleted still keeps its id in the result.
			Row("alpaka-weg", "shared-1", "shared-1", "Scheren", new DateTimeOffset(2025, 5, 2, 0, 0, 0, TimeSpan.Zero)),
			// A legacy row written before SharedEventId existed falls back to its RowKey.
			Row("alpaka-1", "legacy-1", string.Empty, "Impfen", new DateTimeOffset(2025, 6, 1, 0, 0, 0, TimeSpan.Zero)));

		Events.GetHandler handler = new(store, new StubAlpakaLookup(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			["alpaka-1"] = "Richard",
			["alpaka-2"] = "Ludwig"
		}));

		IReadOnlyList<Events.EventResult> events = await handler.HandleAsync(new Events.GetQuery(), TestContext.Current.CancellationToken);

		Assert.Equal(2, events.Count);
		// Newest first.
		Assert.Equal(["legacy-1", "shared-1"], events.Select(e => e.Id));

		Events.EventResult scheren = events[1];
		Assert.Equal("Scheren", scheren.EventType);
		Assert.Equal("2025-05-02", scheren.EventDate);
		Assert.Equal(["alpaka-1", "alpaka-2", "alpaka-weg"], scheren.AlpakaIds);
		Assert.Equal(["Richard", "Ludwig"], scheren.AlpakaNames);

		Events.EventResult impfen = events[0];
		Assert.Equal("Impfen", impfen.EventType);
		Assert.Equal(["alpaka-1"], impfen.AlpakaIds);
		Assert.Equal(["Richard"], impfen.AlpakaNames);
	}
}
