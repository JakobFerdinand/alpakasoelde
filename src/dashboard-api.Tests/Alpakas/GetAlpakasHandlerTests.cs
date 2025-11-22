using DashboardApi.Features.Alpakas;
using Shared.Persistence.Entities;
using TUnit.Assertions;
using TUnit.Core;

namespace DashboardApi.Tests.Alpakas;

public class GetAlpakasHandlerTests
{
    private sealed class InMemoryReadStore(IReadOnlyList<AlpakaEntity> entities) : IAlpakaReadStore
    {
        public Task<IReadOnlyList<AlpakaEntity>> GetAllAsync(CancellationToken cancellationToken) => Task.FromResult(entities);
    }

    private sealed class StaticSigner(string? value) : IImageUrlSigner
    {
        public string? TrySignReadUrl(string? originalUrl, TimeSpan lifetime) => value;
    }

    [Test]
    public async Task Should_return_items_with_signed_urls()
    {
        var handler = new GetAlpakasHandler(new InMemoryReadStore([
            new AlpakaEntity { Name = "A", Geburtsdatum = "2020", ImageUrl = "http://image" }
        ]), new StaticSigner("signed"));

        var result = await handler.HandleAsync(new GetAlpakasQuery(), CancellationToken.None);

        Assert.That(result.First().ImageUrl).IsEqualTo("signed");
    }
}
