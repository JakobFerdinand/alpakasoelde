using System.Net;
using DashboardApi.Features.Messages;
using NSubstitute;
using TUnit.Assertions;
using TUnit.Core;

namespace DashboardApi.Tests.Messages;

public class DeleteMessageHandlerTests
{
    [Test]
    public async Task Should_return_false_when_not_found()
    {
        var store = Substitute.For<IMessageDeleteStore>();
        store
            .When(s => s.DeleteAsync("missing", Arg.Any<CancellationToken>()))
            .Do(_ => throw new Azure.RequestFailedException((int)HttpStatusCode.NotFound, "not found"));

        var handler = new DeleteMessageHandler(store, Substitute.For<Microsoft.Extensions.Logging.ILogger<DeleteMessageHandler>>());

        var result = await handler.HandleAsync(new DeleteMessageCommand("missing"), CancellationToken.None);

        Assert.That(result).IsFalse();
    }
}
