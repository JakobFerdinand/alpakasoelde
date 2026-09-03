using System.Net;
using Azure;
using DashboardApi.Features.Messages;
using dashboard_api.shared.entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashboardApi.Tests;

public sealed class MessageHandlerTests
{
	private sealed class InMemoryMessageStore(params MessageEntity[] seed) : GetMessages.IReadStore
	{
		public Task<IReadOnlyList<MessageEntity>> GetAllAsync(CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<MessageEntity>>(seed);
	}

	private sealed class ThrowingDeleteStore(int status) : DeleteMessage.IStore
	{
		public Task DeleteAsync(string rowKey, CancellationToken cancellationToken)
			=> throw new RequestFailedException(status, "boom");
	}

	private sealed class RecordingDeleteStore : DeleteMessage.IStore
	{
		public List<string> Deleted { get; } = [];

		public Task DeleteAsync(string rowKey, CancellationToken cancellationToken)
		{
			Deleted.Add(rowKey);
			return Task.CompletedTask;
		}
	}

	private static MessageEntity Message(string rowKey, DateTimeOffset? timestamp, string? phone = null) => new()
	{
		Name = "Anna",
		Email = "anna@example.at",
		Phone = phone,
		Message = "Wir wollen wandern.",
		Timestamp = timestamp,
		RowKey = rowKey
	};

	private static DateTimeOffset Day(int day) => new(2025, 6, day, 0, 0, 0, TimeSpan.Zero);

	[Fact]
	public async Task Messages_come_back_newest_first_with_a_missing_phone_as_an_empty_string()
	{
		GetMessages.Handler handler = new(new InMemoryMessageStore(
			Message("m-2", Day(2), phone: "+43 660 1234567"),
			Message("m-3", Day(3)),
			Message("m-1", Day(1))));

		IReadOnlyList<GetMessages.DashboardMessage> messages = await handler.HandleAsync(new GetMessages.Query(), TestContext.Current.CancellationToken);

		Assert.Equal(["m-3", "m-2", "m-1"], messages.Select(m => m.Id));
		Assert.Equal(string.Empty, messages[0].Phone);
		Assert.Equal("+43 660 1234567", messages[1].Phone);
	}

	[Fact]
	public async Task A_message_without_a_timestamp_sorts_last_instead_of_throwing()
	{
		GetMessages.Handler handler = new(new InMemoryMessageStore(
			Message("m-ohne", timestamp: null),
			Message("m-1", Day(1))));

		IReadOnlyList<GetMessages.DashboardMessage> messages = await handler.HandleAsync(new GetMessages.Query(), TestContext.Current.CancellationToken);

		Assert.Equal(["m-1", "m-ohne"], messages.Select(m => m.Id));
	}

	[Fact]
	public async Task Deleting_a_message_that_is_already_gone_reports_false_rather_than_failing()
	{
		DeleteMessage.Handler handler = new(new ThrowingDeleteStore((int)HttpStatusCode.NotFound), NullLogger<DeleteMessage.Handler>.Instance);

		Assert.False(await handler.HandleAsync(new DeleteMessage.Command("m-1"), TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task A_storage_failure_other_than_not_found_is_not_swallowed()
	{
		DeleteMessage.Handler handler = new(new ThrowingDeleteStore((int)HttpStatusCode.ServiceUnavailable), NullLogger<DeleteMessage.Handler>.Instance);

		await Assert.ThrowsAsync<RequestFailedException>(
			() => handler.HandleAsync(new DeleteMessage.Command("m-1"), TestContext.Current.CancellationToken));
	}

	[Fact]
	public async Task Deleting_an_existing_message_passes_the_row_key_through()
	{
		RecordingDeleteStore store = new();
		DeleteMessage.Handler handler = new(store, NullLogger<DeleteMessage.Handler>.Instance);

		Assert.True(await handler.HandleAsync(new DeleteMessage.Command("m-1"), TestContext.Current.CancellationToken));
		Assert.Equal(["m-1"], store.Deleted);
	}

	[Fact]
	public async Task The_old_message_count_only_counts_messages_past_the_threshold()
	{
		DateTimeOffset now = DateTimeOffset.UtcNow;
		GetOldMessageCount.Handler handler = new(new InMemoryMessageStore(
			Message("frisch", now.AddDays(-10)),
			Message("alt", now.AddDays(-200)),
			Message("aelter", now.AddDays(-400)),
			Message("ohne-zeitstempel", timestamp: null)));

		GetOldMessageCount.Result result = await handler.HandleAsync(
			new GetOldMessageCount.Query(TimeSpan.FromDays(180)),
			TestContext.Current.CancellationToken);

		Assert.Equal(2, result.Count);
	}
}
