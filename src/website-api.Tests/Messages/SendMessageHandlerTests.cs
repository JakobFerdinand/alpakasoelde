using Microsoft.Extensions.Configuration;
using NSubstitute;
using Shared.Persistence.Entities;
using TUnit.Assertions;
using TUnit.Core;
using WebsiteApi.Features.Messages;

namespace WebsiteApi.Tests.Messages;

public class SendMessageHandlerTests
{
    [Test]
    public async Task Should_store_message_when_valid()
    {
        var store = Substitute.For<IMessageWriteStore>();
        var emailSender = Substitute.For<IEmailSender>();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [WebsiteApi.Shared.EnvironmentVariables.EmailSenderAddress] = "sender@test.com",
            [WebsiteApi.Shared.EnvironmentVariables.ReceiverEmailAddresses] = "receiver@test.com",
            [WebsiteApi.Shared.EnvironmentVariables.EmailConnection] = "Endpoint=sb://fake"
        }).Build();

        var handler = new SendMessageHandler(store, emailSender, Substitute.For<Microsoft.Extensions.Logging.ILogger<SendMessageHandler>>(), configuration);

        var (result, validation) = await handler.HandleAsync(new SendMessageCommand("Name", "email@test.com", "Hello", true), CancellationToken.None);

        Assert.That(validation).IsNull();
        await store.Received().AddAsync(Arg.Any<MessageEntity>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Should_fail_validation_when_missing_fields()
    {
        var handler = new SendMessageHandler(Substitute.For<IMessageWriteStore>(), Substitute.For<IEmailSender>(), Substitute.For<Microsoft.Extensions.Logging.ILogger<SendMessageHandler>>(), new ConfigurationBuilder().Build());

        var (result, validation) = await handler.HandleAsync(new SendMessageCommand(string.Empty, string.Empty, string.Empty, false), CancellationToken.None);

        Assert.That(validation).IsNotNull();
    }
}
