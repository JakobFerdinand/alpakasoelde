using Microsoft.Extensions.Configuration;
using NSubstitute;
using TUnit.Assertions;
using TUnit.Core;
using website_api.shared.entities;
using WebsiteApi.Features.Messages;
using SendMessageFeature = WebsiteApi.Features.Messages.SendMessage;

namespace WebsiteApi.Tests.Messages;

public class SendMessageHandlerTests
{
	[Test]
	public async Task Should_store_message_when_valid()
	{
		var store = Substitute.For<SendMessageFeature.IMessageWriteStore>();
		var emailSender = Substitute.For<SendMessageFeature.IEmailSender>();
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
		{
			[WebsiteApi.Shared.EnvironmentVariables.EmailSenderAddress] = "sender@test.com",
			[WebsiteApi.Shared.EnvironmentVariables.ReceiverEmailAddresses] = "receiver@test.com",
			[WebsiteApi.Shared.EnvironmentVariables.EmailConnection] = "Endpoint=sb://fake"
		}).Build();

		var handler = new SendMessageFeature.Handler(store, emailSender, Substitute.For<Microsoft.Extensions.Logging.ILogger<SendMessageFeature.Handler>>(), configuration);

		var (result, validation) = await handler.HandleAsync(new SendMessageFeature.Command("Name", "email@test.com", "Hello", true), CancellationToken.None);

		await Assert.That(validation).IsNull();
		await store.Received().AddAsync(Arg.Any<MessageEntity>(), Arg.Any<CancellationToken>());
	}

	[Test]
	public async Task Should_fail_validation_when_missing_fields()
	{
		var handler = new SendMessageFeature.Handler(Substitute.For<SendMessageFeature.IMessageWriteStore>(), Substitute.For<SendMessageFeature.IEmailSender>(), Substitute.For<Microsoft.Extensions.Logging.ILogger<SendMessageFeature.Handler>>(), new ConfigurationBuilder().Build());

		var (result, validation) = await handler.HandleAsync(new SendMessageFeature.Command(string.Empty, string.Empty, string.Empty, false), CancellationToken.None);

		await Assert.That(validation).IsNotNull();
	}
}
