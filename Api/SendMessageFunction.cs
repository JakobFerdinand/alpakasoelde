using Azure;
using Azure.Communication.Email;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Web;

namespace Api;

public class SendMessageFunction(ILoggerFactory loggerFactory, TableServiceClient tableServiceClient)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SendMessageFunction>();
    private readonly TableServiceClient _tableServiceClient = tableServiceClient;

    private const int NameMaxLength = 100;
    private const int EmailMaxLength = 254;
    private const int MessageMaxLength = 2000;

    private async Task<HttpResponseData?> ValidateFormData(
        HttpRequestData req,
        string? name,
        string? email,
        string? messageContent)
    {
        if (new[]
                {
                    (Value: name,        FieldName: "Name"),
                    (Value: email,       FieldName: "Email"),
                    (Value: messageContent, FieldName: "Message")
                }
                .Where(x => string.IsNullOrWhiteSpace(x.Value))
                .Select(x => x.FieldName)
                .ToList()
                is { Count: > 0 } missingFields)
        {
            _logger.LogWarning(
                "Form submission contained missing fields: {MissingFields}. Name: '{Name}', Email: '{Email}', Message: '{Message}'",
                string.Join(", ", missingFields), name, email, messageContent);

            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteAsJsonAsync(new
            {
                title = "Bad Request",
                status = (int)HttpStatusCode.BadRequest,
                detail = $"{string.Join(", ", missingFields)} are required fields and must be provided."
            }).ConfigureAwait(false);

            return badRequestResponse;
        }

        var errors = new[]
        {
            name           is { Length: > NameMaxLength }        ? $"Name exceeds {NameMaxLength} characters."        : null,
            email          is { Length: > EmailMaxLength }       ? $"Email exceeds {EmailMaxLength} characters."       : null,
            messageContent is { Length: > MessageMaxLength }     ? $"Message exceeds {MessageMaxLength} characters."   : null
        }
        .Where(errorMsg => errorMsg is not null)
        .ToList();

        if (errors.Count is > 0)
        {
            _logger.LogWarning(
                "Form submission exceeded field limits. Errors: {Errors}. Name length: {NameLen}, Email length: {EmailLen}, Message length: {MessageLen}",
                string.Join(" | ", errors),
                name!.Length, email!.Length, messageContent!.Length
            );

            var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequestResponse.WriteAsJsonAsync(new
            {
                title = "Bad Request",
                status = (int)HttpStatusCode.BadRequest,
                detail = string.Join(" ", errors)
            }).ConfigureAwait(false);

            return badRequestResponse;
        }

        return null;
    }

    private async Task<EmailSendOperation> SendEmail(
        (string Name, string Email, string Message) messageData)
    {
        string plainTextContent = $"""
            Neue Kontaktanfrage über alpakasoelde.at

            Name: {messageData.Name}
            E-Mail: {messageData.Email}
            Nachricht: {messageData.Message}
            """;

        string htmlContent = $"""
            <html>
                <body>
                    <h1>Neue Kontaktanfrage über alpakasoelde.at</h1>
                    <p><strong>Name:</strong> {messageData.Name}</p>
                    <p><strong>E-Mail:</strong> {messageData.Email}</p>
                    <p><strong>Nachricht:</strong> {messageData.Message}</p>
                </body>
            </html>
            """;

        string? senderEmail = Environment.GetEnvironmentVariable(EnvironmentVariables.EmailSenderAddress);
        var receiverEmails = Environment.GetEnvironmentVariable(EnvironmentVariables.ReceiverEmailAddresses)!
            .Split(';')
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .ToArray();
        _logger.LogInformation(
            "Sending email from '{SenderEmail}' to {ReceiverEmailsCount} recipients: {ReceiverEmails}",
            senderEmail, receiverEmails.Length, string.Join(", ", receiverEmails));
        var receinverEmailList = receiverEmails
            .Select(email => new EmailAddress(email.Trim()))
            .ToArray();

        string? emailConnection = Environment.GetEnvironmentVariable(EnvironmentVariables.EmailConnection);
        EmailClient emailClient = new(emailConnection);
        EmailMessage emailMessage = new(
            senderAddress: senderEmail,
            content: new EmailContent("Neue Kontaktanfrage über alpakasoelde.at")
            {
                PlainText = plainTextContent,
                Html = htmlContent
            },
            recipients: new EmailRecipients(bcc: receinverEmailList));


        return await emailClient.SendAsync(
            WaitUntil.Completed,
            emailMessage);
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
        _logger.LogInformation("Parsed form data - Name: '{Name}', Email: '{Email}', Message: '{Message}'", name, email, messageContent);

        HttpResponseData? validationResult = await ValidateFormData(req, name, email, messageContent);
        if (validationResult is not null)
        {
            return validationResult;
        }

        MessageEntity messageEntity = new()
        {
            Name = name!,
            Email = email!,
            Message = messageContent!
        };

        TableClient tableClient = _tableServiceClient.GetTableClient("messages");
        await tableClient.AddEntityAsync(messageEntity).ConfigureAwait(false);

        await SendEmail((name!, email!, messageContent!)).ConfigureAwait(false);

        var response = req.CreateResponse(HttpStatusCode.SeeOther);
        response.Headers.Add("Location", "/nachricht-gesendet");

        return response;
    }
}
