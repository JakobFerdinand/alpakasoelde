using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using WebsiteApi.Features.Messages;
using WebsiteApi.Shared;
using website_api.shared.entities;

namespace WebsiteApi.Tests;

public sealed class SendMessageHandlerTests
{
	private sealed class RecordingStore : SendMessage.IMessageWriteStore
	{
		public List<MessageEntity> Written { get; } = [];

		public Task AddAsync(MessageEntity entity, CancellationToken cancellationToken)
		{
			Written.Add(entity);
			return Task.CompletedTask;
		}
	}

	private sealed record SentEmail(string Sender, string[] Recipients, string Subject, string PlainText, string Html);

	private sealed class RecordingEmailSender : SendMessage.IEmailSender
	{
		public List<SentEmail> Sent { get; } = [];

		public Task SendAsync(string senderEmail, IEnumerable<string> recipients, string subject, string plainText, string html, CancellationToken cancellationToken)
		{
			Sent.Add(new SentEmail(senderEmail, [.. recipients], subject, plainText, html));
			return Task.CompletedTask;
		}
	}

	private sealed class StubSpamClassifier(bool isSpam) : ISpamClassifier
	{
		public Task<bool> IsSpamAsync(string name, string email, string message, CancellationToken cancellationToken)
			=> Task.FromResult(isSpam);
	}

	private static IConfiguration Configuration() => new ConfigurationBuilder()
		.AddInMemoryCollection(new Dictionary<string, string?>
		{
			[EnvironmentVariables.EmailSenderAddress] = "noreply@alpakasoelde.at",
			// Deliberately ragged: the handler has to split, trim and drop the blank.
			[EnvironmentVariables.ReceiverEmailAddresses] = "hof@alpakasoelde.at; kontakt@alpakasoelde.at ;;",
		})
		.Build();

	private static (SendMessage.Handler Handler, RecordingStore Store, RecordingEmailSender Emails) CreateHandler(bool isSpam = false)
	{
		RecordingStore store = new();
		RecordingEmailSender emails = new();
		SendMessage.Handler handler = new(
			store,
			emails,
			new StubSpamClassifier(isSpam),
			NullLogger<SendMessage.Handler>.Instance,
			Configuration());
		return (handler, store, emails);
	}

	[Fact]
	public async Task Legit_message_is_stored_and_emailed_to_every_configured_receiver()
	{
		var (handler, store, emails) = CreateHandler();

		var (result, validation) = await handler.HandleAsync(
			new SendMessage.Command("  Anna Huber  ", " anna@example.at ", "  Wir wollen wandern.  ", " +43 660 1234567 "),
			TestContext.Current.CancellationToken);

		Assert.Null(validation);
		Assert.NotNull(result);
		Assert.Equal("/nachricht-gesendet", result.RedirectLocation);

		MessageEntity stored = Assert.Single(store.Written);
		Assert.Equal("ContactPartition", stored.PartitionKey);
		Assert.Equal("Anna Huber", stored.Name);
		Assert.Equal("anna@example.at", stored.Email);
		Assert.Equal("Wir wollen wandern.", stored.Message);
		Assert.Equal("+43 660 1234567", stored.Phone);
		Assert.False(stored.IsSpam);

		SentEmail sent = Assert.Single(emails.Sent);
		Assert.Equal("noreply@alpakasoelde.at", sent.Sender);
		Assert.Equal(["hof@alpakasoelde.at", "kontakt@alpakasoelde.at"], sent.Recipients);
		Assert.Equal("Neue Kontaktanfrage über alpakasoelde.at", sent.Subject);
		Assert.Contains("Anna Huber", sent.PlainText);
		Assert.Contains("Telefon: +43 660 1234567", sent.PlainText);
		Assert.Contains("<strong>Telefon:</strong> +43 660 1234567", sent.Html);
	}

	[Fact]
	public async Task Message_without_a_phone_number_leaves_the_phone_line_out()
	{
		var (handler, store, emails) = CreateHandler();

		var (_, validation) = await handler.HandleAsync(
			new SendMessage.Command("Anna", "anna@example.at", "Wir wollen wandern.", ""),
			TestContext.Current.CancellationToken);

		Assert.Null(validation);
		Assert.Null(Assert.Single(store.Written).Phone);

		SentEmail sent = Assert.Single(emails.Sent);
		Assert.DoesNotContain("Telefon", sent.PlainText);
		Assert.DoesNotContain("Telefon", sent.Html);
	}

	[Fact]
	public async Task Spam_is_stored_but_never_emailed_and_still_redirects()
	{
		var (handler, store, emails) = CreateHandler(isSpam: true);

		var (result, validation) = await handler.HandleAsync(
			new SendMessage.Command("Bot", "bot@example.com", "Buy cheap traffic now", ""),
			TestContext.Current.CancellationToken);

		Assert.Null(validation);
		// The sender must not be able to tell spam from a delivered message.
		Assert.Equal("/nachricht-gesendet", result!.RedirectLocation);
		Assert.True(Assert.Single(store.Written).IsSpam);
		Assert.Empty(emails.Sent);
	}

	[Fact]
	public async Task Missing_fields_are_reported_together_and_nothing_is_written()
	{
		var (handler, store, emails) = CreateHandler();

		var (result, validation) = await handler.HandleAsync(
			new SendMessage.Command("", "   ", "", "+43 660 1234567"),
			TestContext.Current.CancellationToken);

		Assert.Null(result);
		Assert.NotNull(validation);
		Assert.Equal(["Name", "Email", "Message"], validation.Errors);
		Assert.Equal("Name, Email, Message are required fields and must be provided.", validation.Detail);
		Assert.Empty(store.Written);
		Assert.Empty(emails.Sent);
	}

	[Fact]
	public async Task Oversized_fields_are_reported_together_and_nothing_is_written()
	{
		var (handler, store, emails) = CreateHandler();

		var (result, validation) = await handler.HandleAsync(
			new SendMessage.Command(new string('n', 101), new string('e', 255), new string('m', 2001), new string('p', 31)),
			TestContext.Current.CancellationToken);

		Assert.Null(result);
		Assert.NotNull(validation);
		Assert.Equal(
			"Name exceeds 100 characters. Email exceeds 254 characters. Phone exceeds 30 characters. Message exceeds 2000 characters.",
			validation.Detail);
		Assert.Empty(store.Written);
		Assert.Empty(emails.Sent);
	}
}
