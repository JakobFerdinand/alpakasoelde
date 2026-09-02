using DashboardApi.Features.PageViews;
using dashboard_api.shared.entities;

namespace DashboardApi.Tests.Fakes;

internal sealed class InMemoryPageViewReadStore(params PageViewEntity[] seed) : GetPageViewStats.IPageViewReadStore
{
	private readonly List<PageViewEntity> _entities = [.. seed];

	public Task<IReadOnlyList<PageViewEntity>> GetAllAsync(CancellationToken cancellationToken)
		=> Task.FromResult<IReadOnlyList<PageViewEntity>>(_entities);

	public static PageViewEntity View(
		DateTimeOffset timestamp,
		string path,
		string rowKey,
		string? sessionId = null,
		string? visitorId = null,
		int viewportWidth = 1280,
		string? referrerHost = null,
		string? navigationType = null) => new()
		{
			Timestamp = timestamp,
			Path = path,
			RowKey = rowKey,
			PartitionKey = $"Pv|{timestamp:yyyy-MM-dd}",
			SessionId = sessionId,
			VisitorId = visitorId,
			ViewportWidth = viewportWidth,
			ReferrerHost = referrerHost,
			NavigationType = navigationType
		};
}
