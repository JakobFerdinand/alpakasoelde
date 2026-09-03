using WebsiteApi.Features.PageViews;
using website_api.shared.entities;

namespace WebsiteApi.Tests;

public sealed class PageViewHandlerTests
{
	private sealed class RecordingStore : PageView.IPageViewWriteStore
	{
		public List<PageViewEntity> Written { get; } = [];

		public Task AddAsync(PageViewEntity entity, CancellationToken cancellationToken)
		{
			Written.Add(entity);
			return Task.CompletedTask;
		}
	}

	[Fact]
	public async Task Accepted_pageview_is_stored_on_the_day_partition_with_a_sanitized_payload()
	{
		RecordingStore store = new();
		PageView.Handler handler = new(store);

		PageView.ValidationProblem? problem = await handler.HandleAsync(
			new PageView.Command("/alpaka-wanderungen/", "google.at", 1280, new string('s', 65), "visitor-1", "reload"),
			TestContext.Current.CancellationToken);

		Assert.Null(problem);
		PageViewEntity entity = Assert.Single(store.Written);
		Assert.Equal($"Pv|{DateTime.UtcNow:yyyy-MM-dd}", entity.PartitionKey);
		Assert.Equal("/alpaka-wanderungen", entity.Path);
		Assert.Equal("google.at", entity.ReferrerHost);
		Assert.Equal(1280, entity.ViewportWidth);
		// A session id past the 64 character budget is dropped rather than stored truncated.
		Assert.Null(entity.SessionId);
		Assert.Equal("visitor-1", entity.VisitorId);
		Assert.Equal("reload", entity.NavigationType);
	}

	[Theory]
	[InlineData("alpakas", "Path must start with '/'.")]
	[InlineData("", "Path are required fields and must be provided.")]
	public async Task Rejected_pageview_reports_the_problem_and_writes_nothing(string path, string expectedDetail)
	{
		RecordingStore store = new();
		PageView.Handler handler = new(store);

		PageView.ValidationProblem? problem = await handler.HandleAsync(
			new PageView.Command(path, null, 0, null, null, null),
			TestContext.Current.CancellationToken);

		Assert.NotNull(problem);
		Assert.Equal(expectedDetail, problem.Detail);
		Assert.Empty(store.Written);
	}
}
