using dashboard_api.shared.entities;
using DashboardApi.Features.Alpakas;
using TUnit.Assertions;
using TUnit.Core;
using GetAlpakasFeature = DashboardApi.Features.Alpakas.GetAlpakas;

namespace DashboardApi.Tests.Alpakas;

public class GetAlpakasHandlerTests
{
	private sealed class InMemoryReadStore(IReadOnlyList<AlpakaEntity> entities) : GetAlpakasFeature.IAlpakaReadStore
	{
		public Task<IReadOnlyList<AlpakaEntity>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult(entities);
	}

	private sealed class StaticSigner(string? value) : GetAlpakasFeature.IImageUrlSigner
	{
		public string? TrySignReadUrl(string? originalUrl, TimeSpan lifetime) => value;
	}

	[Test]
	public async Task Should_return_items_with_signed_urls()
	{
		var handler = new GetAlpakasFeature.Handler(new InMemoryReadStore([
			new AlpakaEntity { Name = "A", Geburtsdatum = "2020", ImageUrl = "http://image" }
		]), new StaticSigner("signed"));

		var result = await handler.HandleAsync(new GetAlpakasFeature.Query(), CancellationToken.None);

		await Assert.That(result.First().ImageUrl).IsEqualTo("signed");
	}
}
