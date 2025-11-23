using dashboard_api.shared.entities;
using DashboardApi.Features.Alpakas;
using TUnit.Assertions;
using TUnit.Core;
using GetAlpakaByIdFeature = DashboardApi.Features.Alpakas.GetAlpakaById;
using IImageUrlSigner = DashboardApi.Features.Alpakas.GetAlpakas.IImageUrlSigner;

namespace DashboardApi.Tests.Alpakas;

public class GetAlpakaByIdHandlerTests
{
    private sealed class SingleStore(AlpakaEntity? entity) : GetAlpakaByIdFeature.IReadStore
    {
        public Task<AlpakaEntity?> GetByIdAsync(string id, CancellationToken cancellationToken) =>
            Task.FromResult(entity?.RowKey == id ? entity : null);
    }

    private sealed class PassthroughSigner : IImageUrlSigner
    {
        public string? TrySignReadUrl(string? originalUrl, TimeSpan lifetime) => originalUrl;
    }

    private sealed class FakeEventStore(IReadOnlyList<EventEntity> events) : GetAlpakaByIdFeature.IEventReadStore
    {
        public Task<IReadOnlyList<EventEntity>> GetByAlpakaIdAsync(string alpakaId, CancellationToken cancellationToken)
        {
            IReadOnlyList<EventEntity> scoped = events.Where(e => e.PartitionKey == alpakaId).ToList();
            return Task.FromResult(scoped);
        }
    }

    [Test]
    public async Task Returns_null_when_missing()
    {
        var handler = new GetAlpakaByIdFeature.Handler(new SingleStore(null), new PassthroughSigner(), new FakeEventStore([]));
        var result = await handler.HandleAsync(new GetAlpakaByIdFeature.Query("id"), CancellationToken.None);
        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Maps_alpaka_with_sorted_events()
    {
        var alpaka = new AlpakaEntity
        {
            PartitionKey = "AlpakaPartition",
            RowKey = "alpaka-1",
            Name = "Fluffy",
            Geburtsdatum = "2022-01-10",
            ImageUrl = "https://storage/photos/fluffy.png"
        };

        IReadOnlyList<EventEntity> events =
        [
            new EventEntity
            {
                PartitionKey = "alpaka-1",
                RowKey = "b",
                EventType = "Scheren",
                EventDate = new DateTimeOffset(2024, 06, 01, 0, 0, 0, TimeSpan.Zero),
                Comment = "Sommerhaarschnitt",
                Cost = 75
            },
            new EventEntity
            {
                PartitionKey = "alpaka-1",
                RowKey = "a",
                EventType = "Impfung",
                EventDate = new DateTimeOffset(2024, 05, 15, 0, 0, 0, TimeSpan.Zero),
                Comment = "Routine",
                Cost = 40
            }
        ];

        var handler = new GetAlpakaByIdFeature.Handler(
            new SingleStore(alpaka),
            new PassthroughSigner(),
            new FakeEventStore(events));

        var result = await handler.HandleAsync(new GetAlpakaByIdFeature.Query(alpaka.RowKey), CancellationToken.None);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Id).IsEqualTo(alpaka.RowKey);
        await Assert.That(result.ImageUrl).IsEqualTo(alpaka.ImageUrl);
        await Assert.That(result.Events).HasCount(2);
        await Assert.That(result.Events[0].EventType).IsEqualTo("Scheren");
        await Assert.That(result.Events[0].EventDate).IsEqualTo("2024-06-01");
        await Assert.That(result.Events[1].EventType).IsEqualTo("Impfung");
        await Assert.That(result.Events[1].EventDate).IsEqualTo("2024-05-15");
    }
}
