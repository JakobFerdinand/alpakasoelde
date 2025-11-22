using dashboard_api.shared.entities;
using DashboardApi.Features.Events;
using NSubstitute;
using TUnit.Assertions;
using TUnit.Core;
using EventsFeature = DashboardApi.Features.Events.Events;

namespace DashboardApi.Tests.Events;

public class AddEventHandlerTests
{
	private sealed class InMemoryEventStore : EventsFeature.IEventStore
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
		var handler = new EventsFeature.AddHandler(store, Substitute.For<Microsoft.Extensions.Logging.ILogger<EventsFeature.AddHandler>>());

		var (result, error) = await handler.HandleAsync(new EventsFeature.AddCommand(string.Empty, ["alpaka"], "2023-01-01", 0, null), CancellationToken.None);

		await Assert.That(error).IsNotNull();
	}
}
