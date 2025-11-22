using DashboardApi.Features.Alpakas;
using Shared.Persistence.Entities;
using TUnit.Assertions;
using TUnit.Core;

namespace DashboardApi.Tests.Alpakas;

public class GetAlpakaByIdHandlerTests
{
    private sealed class SingleStore(AlpakaEntity? entity) : IAlpakaByIdReadStore
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
        var handler = new GetAlpakaByIdHandler(new SingleStore(null), new PassthroughSigner());
        var result = await handler.HandleAsync(new GetAlpakaByIdQuery("id"), CancellationToken.None);
        Assert.That(result).IsNull();
    }
}
