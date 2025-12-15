using System.Net;
using DashboardApi.Features.Messages;
using NSubstitute;
using TUnit.Assertions;
using TUnit.Core;
using DeleteMessageFeature = DashboardApi.Features.Messages.DeleteMessage;

namespace DashboardApi.Tests.Messages;

public class DeleteMessageHandlerTests
{
	[Test]
	public async Task Should_return_false_when_not_found()
	{
		var store = Substitute.For<DeleteMessageFeature.IStore>();
		store
			.When(s => s.DeleteAsync("missing", Arg.Any<CancellationToken>()))
			.Do(_ => throw new Azure.RequestFailedException((int)HttpStatusCode.NotFound, "not found"));

		var handler = new DeleteMessageFeature.Handler(store, Substitute.For<Microsoft.Extensions.Logging.ILogger<DeleteMessageFeature.Handler>>());

		var result = await handler.HandleAsync(new DeleteMessageFeature.Command("missing"), CancellationToken.None);

		await Assert.That(result).IsFalse();
	}
}
