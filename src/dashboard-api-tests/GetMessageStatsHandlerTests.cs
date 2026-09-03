using DashboardApi.Features.Messages;
using dashboard_api.shared.entities;

namespace DashboardApi.Tests;

public sealed class GetMessageStatsHandlerTests
{
	private sealed class InMemoryMessageStore(params MessageEntity[] seed) : GetMessages.IReadStore
	{
		public Task<IReadOnlyList<MessageEntity>> GetAllAsync(CancellationToken cancellationToken)
			=> Task.FromResult<IReadOnlyList<MessageEntity>>(seed);
	}

	// The handler reads the clock itself, so the fixtures are placed relative to now.
	private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

	private static MessageEntity Message(int daysAgo, bool isSpam) => new()
	{
		Name = "Anna",
		Email = "anna@example.at",
		Message = "Wir wollen wandern.",
		IsSpam = isSpam,
		Timestamp = Now.AddDays(-daysAgo),
		RowKey = $"m-{daysAgo}"
	};

	private static Task<GetMessageStats.Result> Handle(int days, params MessageEntity[] messages)
		=> new GetMessageStats.Handler(new InMemoryMessageStore(messages)).HandleAsync(
			new GetMessageStats.Query(days),
			TestContext.Current.CancellationToken);

	[Fact]
	public async Task Spam_and_legit_are_split_inside_the_window_while_old_counts_the_whole_table()
	{
		GetMessageStats.Result result = await Handle(
			28,
			Message(1, isSpam: false),
			Message(2, isSpam: true),
			Message(3, isSpam: true),
			// Outside the 28 day window, and past the six month retention mark.
			Message(200, isSpam: false));

		Assert.Equal(3, result.Total);
		Assert.Equal(2, result.Spam);
		Assert.Equal(1, result.Legit);
		Assert.Equal(1, result.OldCount);
	}

	[Fact]
	public async Task Messages_just_short_of_six_months_are_not_old_yet()
	{
		GetMessageStats.Result result = await Handle(28, Message(179, isSpam: false));

		Assert.Equal(0, result.OldCount);
		// Still outside the reporting window.
		Assert.Equal(0, result.Total);
	}

	[Fact]
	public async Task The_weekly_series_spans_the_window_and_accounts_for_every_message_in_it()
	{
		GetMessageStats.Result result = await Handle(
			28,
			Message(1, isSpam: false),
			Message(2, isSpam: true),
			Message(20, isSpam: false));

		Assert.Equal(result.Spam, result.Series.Sum(b => b.Spam));
		Assert.Equal(result.Legit, result.Series.Sum(b => b.Legit));

		List<DateTime> weeks = [.. result.Series.Select(b => DateTime.Parse(b.Period))];
		Assert.All(weeks, week => Assert.Equal(DayOfWeek.Monday, week.DayOfWeek));
		Assert.Equal(weeks.OrderBy(w => w), weeks);
		Assert.Equal(weeks.Distinct(), weeks);
	}

	[Fact]
	public async Task An_empty_table_still_yields_a_series()
	{
		GetMessageStats.Result result = await Handle(28);

		Assert.Equal(0, result.Total);
		Assert.NotEmpty(result.Series);
		Assert.All(result.Series, bucket =>
		{
			Assert.Equal(0, bucket.Spam);
			Assert.Equal(0, bucket.Legit);
		});
	}
}
