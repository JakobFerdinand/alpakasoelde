using DashboardApi.Features.Events;
using NSubstitute;
using Shared.Persistence.Entities;
using TUnit.Assertions;
using TUnit.Core;

namespace DashboardApi.Tests.Events;

public class AddEventHandlerTests
{
    private sealed class InMemoryEventStore : IEventStore
    {
        public List<EventEntity> Added { get; } = [];
        public Task AddAsync(EventEntity entity, CancellationToken cancellationToken)
        {
            Added.Add(entity);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<EventEntity>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<EventEntity>>(Added);
    }

    [Test]
    public async Task Returns_error_when_event_type_missing()
    {
        var store = new InMemoryEventStore();
        var handler = new AddEventHandler(store, Substitute.For<Microsoft.Extensions.Logging.ILogger<AddEventHandler>>());

        var (result, error) = await handler.HandleAsync(new AddEventCommand(string.Empty, ["alpaka"], "2023-01-01", 0, null), CancellationToken.None);

        Assert.That(error).IsNotNull();
    }
}
