using dashboard_api.shared.entities;
using DashboardApi.Features.Messages;
using TUnit.Assertions;
using TUnit.Core;
using GetMessagesFeature = DashboardApi.Features.Messages.GetMessages;
using GetOldMessageCountFeature = DashboardApi.Features.Messages.GetOldMessageCount;

namespace DashboardApi.Tests.Messages;

public class MessageHandlerTests
{
	private sealed class InMemoryMessageStore(IReadOnlyList<MessageEntity> entities) : GetMessagesFeature.IReadStore
	{
		public Task<IReadOnlyList<MessageEntity>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult(entities);
	}

	[Test]
	public async Task GetMessages_returns_ordered_items()
	{
		var handler = new GetMessagesFeature.Handler(new InMemoryMessageStore([
			new MessageEntity { Name = "B", Email = "b@test.com", Message = "hi", Timestamp = DateTimeOffset.UtcNow.AddDays(-1) },
			new MessageEntity { Name = "A", Email = "a@test.com", Message = "hello", Timestamp = DateTimeOffset.UtcNow }
		]));

		var result = await handler.HandleAsync(new GetMessagesFeature.Query(), CancellationToken.None);

		await Assert.That(result.First().Name).IsEqualTo("A");
	}

	[Test]
	public async Task GetOldMessageCount_respects_threshold()
	{
		var threshold = TimeSpan.FromDays(1);
		var handler = new GetOldMessageCountFeature.Handler(new InMemoryMessageStore([
			new MessageEntity { Name = "Old", Email = "o@test.com", Message = "old", Timestamp = DateTimeOffset.UtcNow.AddDays(-2) },
			new MessageEntity { Name = "New", Email = "n@test.com", Message = "new", Timestamp = DateTimeOffset.UtcNow }
		]));

		var result = await handler.HandleAsync(new GetOldMessageCountFeature.Query(threshold), CancellationToken.None);

		await Assert.That(result.Count).IsEqualTo(1);
	}
}
