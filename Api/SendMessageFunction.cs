using Azure;
using Azure.Data.Tables;
using Azure.Communication.Email;
using Azure.Communication.Email.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Web;

namespace Api;

public sealed record MessageEntity(string Name, string Email, string Message) : ITableEntity
{
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string PartitionKey { get; set; } = "ContactPartition";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
}


public class SendMessageFunction(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SendMessageFunction>();

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

        MessageEntity messageEntity = new(name!, email!, messageContent!);

        string? connectionString = Environment.GetEnvironmentVariable("MessageStorageConnection");
        TableClient tableClient = new(connectionString, "messages");
        await tableClient.AddEntityAsync(messageEntity).ConfigureAwait(false);

        string? emailConnectionString = Environment.GetEnvironmentVariable("EmailConnectionString");
        string? emailSender = Environment.GetEnvironmentVariable("EmailSender");
        string? emailRecipient = Environment.GetEnvironmentVariable("EmailRecipient");

        if (!string.IsNullOrWhiteSpace(emailConnectionString) &&
            !string.IsNullOrWhiteSpace(emailSender) &&
            !string.IsNullOrWhiteSpace(emailRecipient))
        {
            try
            {
                EmailClient emailClient = new(emailConnectionString);

                EmailContent content = new("New contact form submission")
                {
                    PlainText = $"Name: {name}\nEmail: {email}\nMessage:\n{messageContent}"
                };

                EmailRecipients recipients = new(new[] { new EmailAddress(emailRecipient) });

                EmailMessage emailMessage = new(emailSender, recipients, content);

                await emailClient.SendAsync(WaitUntil.Completed, emailMessage).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification email.");
            }
        }
        else
        {
            _logger.LogWarning("Email environment variables are not fully configured. Email not sent.");
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.SeeOther);
        response.Headers.Add("Location", "/nachricht-gesendet");

        return response;
    }
}
