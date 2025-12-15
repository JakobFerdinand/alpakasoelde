using System.Net;
using System.Web;
using Azure;
using Azure.Communication.Email;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebsiteApi.Shared;
using website_api.shared.entities;

namespace WebsiteApi.Features.Messages;

public sealed class SendMessage
{
	private readonly Handler _handler;
	private readonly ILogger<SendMessage> _logger;

	public SendMessage(Handler handler, ILogger<SendMessage> logger)
	{
		_handler = handler;
		_logger = logger;
	}

	[Function("send-message")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
		FunctionContext context)
	{
		string? body = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);
		_logger.LogInformation("Received data: {Body}", body);

		var parsedForm = HttpUtility.ParseQueryString(body);
		string? name = parsedForm["name"]?.Trim();
		string? email = parsedForm["email"]?.Trim();
		string? messageContent = parsedForm["message"]?.Trim();
		bool privacyAccepted = parsedForm["privacyConsent"]?.Trim().ToLowerInvariant() switch
		{
			"on" or "true" or "1" => true,
			_ => false
		};

		var (result, validation) = await _handler.HandleAsync(new Command(name ?? string.Empty, email ?? string.Empty, messageContent ?? string.Empty, privacyAccepted), req.FunctionContext.CancellationToken);
		if (validation is not null)
		{
			_logger.LogWarning("Form submission validation failed: {Detail}", validation.Detail);
			var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
			await badRequestResponse.WriteAsJsonAsync(new
			{
				title = "Bad Request",
				status = (int)HttpStatusCode.BadRequest,
				detail = validation.Detail
			}).ConfigureAwait(false);
			return badRequestResponse;
		}

		var response = req.CreateResponse(HttpStatusCode.SeeOther);
		response.Headers.Add("Location", result!.RedirectLocation);
		return response;
	}

	public sealed record Command(string Name, string Email, string Message, bool PrivacyAccepted);

	public sealed record Result(string RedirectLocation);

	public sealed record ValidationProblem(IReadOnlyCollection<string> Errors, string Detail);

	public interface IMessageWriteStore
	{
		Task AddAsync(MessageEntity entity, CancellationToken cancellationToken);
	}

	public interface IEmailSender
	{
		Task SendAsync(string senderEmail, IEnumerable<string> recipients, string subject, string plainText, string html, CancellationToken cancellationToken);
	}

	public sealed class TableMessageWriteStore(TableServiceClient tableServiceClient) : IMessageWriteStore
	{
		private readonly TableServiceClient _tableServiceClient = tableServiceClient;

		public Task AddAsync(MessageEntity entity, CancellationToken cancellationToken)
		{
			TableClient tableClient = _tableServiceClient.GetTableClient("messages");
			return tableClient.AddEntityAsync(entity, cancellationToken);
		}
	}

	public sealed class EmailSender(IConfiguration configuration) : IEmailSender
	{
		private readonly IConfiguration _configuration = configuration;

		public async Task SendAsync(string senderEmail, IEnumerable<string> recipients, string subject, string plainText, string html, CancellationToken cancellationToken)
		{
			string? emailConnection = _configuration[EnvironmentVariables.EmailConnection];
			EmailClient emailClient = new(emailConnection);
			EmailMessage emailMessage = new(
				senderAddress: senderEmail,
				content: new EmailContent(subject)
				{
					PlainText = plainText,
					Html = html
				},
				recipients: new EmailRecipients(bcc: recipients.Select(email => new EmailAddress(email.Trim())).ToArray()));

			await emailClient.SendAsync(WaitUntil.Completed, emailMessage, cancellationToken: cancellationToken).ConfigureAwait(false);
		}
	}

	public sealed class Handler(IMessageWriteStore store, IEmailSender emailSender, ILogger<Handler> logger, IConfiguration configuration)
	{
		private readonly IMessageWriteStore _store = store;
		private readonly IEmailSender _emailSender = emailSender;
		private readonly ILogger<Handler> _logger = logger;
		private readonly IConfiguration _configuration = configuration;

		private const int NameMaxLength = 100;
		private const int EmailMaxLength = 254;
		private const int MessageMaxLength = 2000;

		public async Task<(Result? Result, ValidationProblem? Validation)> HandleAsync(Command command, CancellationToken cancellationToken)
		{
			List<string> missingFields = [];
			if (string.IsNullOrWhiteSpace(command.Name)) missingFields.Add("Name");
			if (string.IsNullOrWhiteSpace(command.Email)) missingFields.Add("Email");
			if (string.IsNullOrWhiteSpace(command.Message)) missingFields.Add("Message");

			if (missingFields.Count > 0)
			{
				return (null, new ValidationProblem(missingFields, $"{string.Join(", ", missingFields)} are required fields and must be provided."));
			}

			List<string> errors = new();
			if (command.Name.Length > NameMaxLength) errors.Add($"Name exceeds {NameMaxLength} characters.");
			if (command.Email.Length > EmailMaxLength) errors.Add($"Email exceeds {EmailMaxLength} characters.");
			if (command.Message.Length > MessageMaxLength) errors.Add($"Message exceeds {MessageMaxLength} characters.");

			if (!command.PrivacyAccepted)
			{
				errors.Add("Bitte bestätige, dass du die Datenschutzerklärung gelesen hast.");
			}

			if (errors.Count > 0)
			{
				return (null, new ValidationProblem(errors, string.Join(" ", errors)));
			}

			MessageEntity messageEntity = new()
			{
				Name = command.Name.Trim(),
				Email = command.Email.Trim(),
				Message = command.Message.Trim(),
				PrivacyPolicyAccepted = command.PrivacyAccepted
			};

			await _store.AddAsync(messageEntity, cancellationToken).ConfigureAwait(false);

			string? senderEmail = _configuration[EnvironmentVariables.EmailSenderAddress];
			var receiverEmails = (_configuration[EnvironmentVariables.ReceiverEmailAddresses] ?? string.Empty)
				.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
				.ToArray();

			string plainTextContent = $"""
            Neue Kontaktanfrage über alpakasoelde.at

            Name: {messageEntity.Name}
            E-Mail: {messageEntity.Email}
            Nachricht: {messageEntity.Message}
            """;

			string htmlContent = $"""
            <html>
                <body>
                    <h1>Neue Kontaktanfrage über alpakasoelde.at</h1>
                    <p><strong>Name:</strong> {messageEntity.Name}</p>
                    <p><strong>E-Mail:</strong> {messageEntity.Email}</p>
                    <p><strong>Nachricht:</strong> {messageEntity.Message}</p>
                </body>
            </html>
            """;

			await _emailSender.SendAsync(senderEmail!, receiverEmails, "Neue Kontaktanfrage über alpakasoelde.at", plainTextContent, htmlContent, cancellationToken).ConfigureAwait(false);

			return (new Result("/nachricht-gesendet"), null);
		}
	}
}
