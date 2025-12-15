using dashboard_api.shared.entities;
using DashboardApi.Features.Alpakas;
using NSubstitute;
using TUnit.Assertions;
using TUnit.Core;
using AddAlpakaFeature = DashboardApi.Features.Alpakas.AddAlpaka;

namespace DashboardApi.Tests.Alpakas;

public class AddAlpakaHandlerTests
{
	[Test]
	public async Task Should_add_entity_when_valid()
	{
		var writeStore = Substitute.For<AddAlpakaFeature.IAlpakaWriteStore>();
		var imageStore = Substitute.For<AddAlpakaFeature.IAlpakaImageStore>();
		var handler = new AddAlpakaFeature.Handler(writeStore, imageStore, Substitute.For<Microsoft.Extensions.Logging.ILogger<AddAlpakaFeature.Handler>>());

		var response = await handler.HandleAsync(new AddAlpakaFeature.Command("Alfi", "2020-01-01", null), CancellationToken.None);

		await Assert.That(response.IsValid).IsTrue();
		await writeStore.Received().AddAsync(Arg.Any<AlpakaEntity>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Should_return_validation_errors_when_missing_fields()
	{
		var handler = new AddAlpakaFeature.Handler(Substitute.For<AddAlpakaFeature.IAlpakaWriteStore>(), Substitute.For<AddAlpakaFeature.IAlpakaImageStore>(), Substitute.For<Microsoft.Extensions.Logging.ILogger<AddAlpakaFeature.Handler>>());

		var response = await handler.HandleAsync(new AddAlpakaFeature.Command(string.Empty, string.Empty, null), CancellationToken.None);

		await Assert.That(response.IsValid).IsFalse();
	}
}
