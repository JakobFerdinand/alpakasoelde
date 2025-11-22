using dashboard_api.shared.entities;
using DashboardApi.Features.Alpakas;
using NSubstitute;
using TUnit.Assertions;
using TUnit.Core;

namespace DashboardApi.Tests.Alpakas;

public class UpdateAlpakaHandlerTests
{
	private sealed class InMemoryUpdateStore(AlpakaEntity? entity)
		: IAlpakaUpdateStore
	{
		public Task<AlpakaEntity?> GetAsync(string id, CancellationToken cancellationToken) => Task.FromResult(entity);
		public Task UpdateAsync(AlpakaEntity entity, Azure.ETag etag, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class NoopImageReplacement : IAlpakaImageReplacementStore
	{
		public Task<string?> ReplaceAsync(string? existingUrl, AlpakaImagePayload newImage, CancellationToken cancellationToken) => Task.FromResult(existingUrl);
	}

	private sealed class StaticSigner : IImageUrlSigner
	{
		public string? TrySignReadUrl(string? originalUrl, TimeSpan lifetime) => originalUrl;
	}

	[Test]
	public async Task Returns_not_found_when_missing()
	{
		var handler = new UpdateAlpakaHandler(new InMemoryUpdateStore(null), new NoopImageReplacement(), Substitute.For<Microsoft.Extensions.Logging.ILogger<UpdateAlpakaHandler>>(), new StaticSigner());
		var response = await handler.HandleAsync(new UpdateAlpakaCommand("missing", "Name", "Date", null), CancellationToken.None);
		await Assert.That(response.NotFound).IsTrue();
	}
}
