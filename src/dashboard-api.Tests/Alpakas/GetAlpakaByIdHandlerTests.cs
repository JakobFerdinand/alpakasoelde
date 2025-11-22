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
		public Task<AlpakaEntity?> GetByIdAsync(string id, CancellationToken cancellationToken) => Task.FromResult(entity);
	}

	private sealed class PassthroughSigner : IImageUrlSigner
	{
		public string? TrySignReadUrl(string? originalUrl, TimeSpan lifetime) => originalUrl;
	}

	[Test]
	public async Task Returns_null_when_missing()
	{
		var handler = new GetAlpakaByIdFeature.Handler(new SingleStore(null), new PassthroughSigner());
		var result = await handler.HandleAsync(new GetAlpakaByIdFeature.Query("id"), CancellationToken.None);
		await Assert.That(result).IsNull();
	}
}
