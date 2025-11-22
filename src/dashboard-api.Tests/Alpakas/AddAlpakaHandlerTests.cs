using DashboardApi.Features.Alpakas;
using NSubstitute;
using Shared.Persistence.Entities;
using TUnit.Assertions;
using TUnit.Core;

namespace DashboardApi.Tests.Alpakas;

public class AddAlpakaHandlerTests
{
    [Test]
    public async Task Should_add_entity_when_valid()
    {
        var writeStore = Substitute.For<IAlpakaWriteStore>();
        var imageStore = Substitute.For<IAlpakaImageStore>();
        var handler = new AddAlpakaHandler(writeStore, imageStore, Substitute.For<Microsoft.Extensions.Logging.ILogger<AddAlpakaHandler>>());

        var response = await handler.HandleAsync(new AddAlpakaCommand("Alfi", "2020-01-01", null), CancellationToken.None);

        Assert.That(response.IsValid).IsTrue();
        await writeStore.Received().AddAsync(Arg.Any<AlpakaEntity>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_return_validation_errors_when_missing_fields()
    {
        var handler = new AddAlpakaHandler(Substitute.For<IAlpakaWriteStore>(), Substitute.For<IAlpakaImageStore>(), Substitute.For<Microsoft.Extensions.Logging.ILogger<AddAlpakaHandler>>());

        var response = await handler.HandleAsync(new AddAlpakaCommand(string.Empty, string.Empty, null), CancellationToken.None);

        Assert.That(response.IsValid).IsFalse();
    }
}
