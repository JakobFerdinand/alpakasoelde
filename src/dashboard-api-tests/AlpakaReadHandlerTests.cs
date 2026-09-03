using DashboardApi.Features.Alpakas;
using DashboardApi.Tests.Fakes;
using dashboard_api.shared.entities;

namespace DashboardApi.Tests;

public sealed class AlpakaReadHandlerTests
{
	private sealed class StubListStore(params AlpakaEntity[] entities) : GetAlpakas.IAlpakaReadStore
	{
		public Task<IReadOnlyList<AlpakaEntity>> GetAllAsync(CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<AlpakaEntity>>(entities);
	}

	private sealed class StubByIdStore(AlpakaEntity? entity) : GetAlpakaById.IReadStore
	{
		public Task<AlpakaEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
			=> Task.FromResult(entity is not null && entity.RowKey == id ? entity : null);
	}

	private sealed class StubEventReadStore(params EventEntity[] events) : GetAlpakaById.IEventReadStore
	{
		public Task<IReadOnlyList<EventEntity>> GetByAlpakaIdAsync(string alpakaId, CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<EventEntity>>([.. events.Where(e => e.PartitionKey == alpakaId)]);
	}

	private static AlpakaEntity Alpaka(string id, string name, string? imageUrl) => new()
	{
		Name = name,
		Geburtsdatum = "2019-04-01",
		ImageUrl = imageUrl,
		RowKey = id
	};

	private static EventEntity Event(string rowKey, string eventType, DateTimeOffset eventDate, double? cost = null, string? comment = null) => new()
	{
		EventType = eventType,
		EventDate = eventDate,
		Cost = cost,
		Comment = comment,
		PartitionKey = "alpaka-1",
		RowKey = rowKey,
		SharedEventId = rowKey
	};

	private static DateTimeOffset Day(int month, int day) => new(2025, month, day, 0, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task Listing_hands_every_stored_image_url_to_the_signer()
	{
		RecordingImageUrlSigner signer = new();
		GetAlpakas.Handler handler = new(
			new StubListStore(
				Alpaka("alpaka-1", "Richard", "https://storage/alpakas/richard.jpg"),
				Alpaka("alpaka-2", "Ludwig", null)),
			signer);

		IReadOnlyList<GetAlpakas.AlpakaListItem> alpakas = await handler.HandleAsync(new GetAlpakas.Query(), TestContext.Current.CancellationToken);

		// Blob urls are never handed out raw — the container is private.
		Assert.Equal(
			[("alpaka-1", "https://storage/alpakas/richard.jpg?sas"), ("alpaka-2", null)],
			alpakas.Select(a => (a.Id, a.ImageUrl)));
		Assert.Equal(
			[("https://storage/alpakas/richard.jpg", TimeSpan.FromMinutes(30)), (null, TimeSpan.FromMinutes(30))],
			signer.Calls);
	}

	[Fact]
	public async Task An_unknown_alpaka_has_no_detail()
	{
		GetAlpakaById.Handler handler = new(
			new StubByIdStore(Alpaka("alpaka-1", "Richard", null)),
			new RecordingImageUrlSigner(),
			new StubEventReadStore());

		Assert.Null(await handler.HandleAsync(new GetAlpakaById.Query("gibt-es-nicht"), TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task The_detail_signs_the_image_and_lists_the_events_newest_first()
	{
		RecordingImageUrlSigner signer = new();
		GetAlpakaById.Handler handler = new(
			new StubByIdStore(Alpaka("alpaka-1", "Richard", "https://storage/alpakas/richard.jpg")),
			signer,
			new StubEventReadStore(
				Event("e-mai", "Scheren", Day(5, 2), cost: 180, comment: "Ganze Herde"),
				Event("e-juni", "Impfen", Day(6, 1)),
				Event("e-april", "Entwurmen", Day(4, 1))));

		GetAlpakaById.Result? detail = await handler.HandleAsync(new GetAlpakaById.Query("alpaka-1"), TestContext.Current.CancellationToken);

		Assert.NotNull(detail);
		Assert.Equal("Richard", detail.Name);
		Assert.Equal("https://storage/alpakas/richard.jpg?sas", detail.ImageUrl);
		Assert.Equal(["e-juni", "e-mai", "e-april"], detail.Events.Select(e => e.Id));
		Assert.Equal(["2025-06-01", "2025-05-02", "2025-04-01"], detail.Events.Select(e => e.EventDate));

		GetAlpakaById.EventResult scheren = detail.Events[1];
		Assert.Equal("Scheren", scheren.EventType);
		Assert.Equal(180, scheren.Cost);
		Assert.Equal("Ganze Herde", scheren.Comment);
	}

	[Fact]
	public async Task The_detail_only_lists_events_of_that_alpaka()
	{
		EventEntity otherAlpakasEvent = Event("e-fremd", "Impfen", Day(6, 1));
		otherAlpakasEvent.PartitionKey = "alpaka-2";

		GetAlpakaById.Handler handler = new(
			new StubByIdStore(Alpaka("alpaka-1", "Richard", null)),
			new RecordingImageUrlSigner(),
			new StubEventReadStore(Event("e-mai", "Scheren", Day(5, 2)), otherAlpakasEvent));

		GetAlpakaById.Result? detail = await handler.HandleAsync(new GetAlpakaById.Query("alpaka-1"), TestContext.Current.CancellationToken);

		Assert.Equal(["e-mai"], detail!.Events.Select(e => e.Id));
	}
}
