using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Collections.Generic;
using System.Web;

namespace Api;

public sealed record MessageEntity(string Name, string Email, string Message) : ITableEntity
{
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string PartitionKey { get; set; } = "ContactPartition";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
}

public sealed class SendMessageOutput
{
    public required HttpResponseData HttpResponse { get; init; }

    [TableOutput("messages", Connection = "MessageStorageConnection")]
    public required MessageEntity? ContactRow { get; init; }
}

public class SendMessageFunction(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<SendMessageFunction>();

    private const int NameMaxLength = 100;
    private const int EmailMaxLength = 254;
    private const int MessageMaxLength = 2000;

    [Function("send-message")]
    public async Task<SendMessageOutput> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
        FunctionContext context)
    {
        // Read the entire request body as a string
        string? body = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);

        _logger.LogInformation("Received data: {Body}", body);

        // Parse the form data from the request body
        var parsedForm = HttpUtility.ParseQueryString(body);
        string? name = parsedForm["name"]?.Trim();
        string? email = parsedForm["email"]?.Trim();
        string? messageContent = parsedForm["message"]?.Trim(); // Use messageContent to avoid potential naming conflicts

        _logger.LogInformation("Parsed form data - Name: '{Name}', Email: '{Email}', Message: '{Message}'", name, email, messageContent);

        // Basic validation for required fields
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(messageContent))
        {
            _logger.LogWarning("Form submission contained missing fields. Name: '{Name}', Email: '{Email}', Message: '{Message}'", name, email, messageContent);

            var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequestResponse.WriteAsJsonAsync(new
            {
                title = "Bad Request",
                status = (int)System.Net.HttpStatusCode.BadRequest,
                detail = "Name, Email, and Message are required fields and must be provided."
            }).ConfigureAwait(false);
            return new SendMessageOutput
            {
                HttpResponse = badRequestResponse,
                ContactRow = null
            };
        }

        var errors = new List<string>();
        if (name!.Length > NameMaxLength)
        {
            errors.Add($"Name exceeds {NameMaxLength} characters.");
        }
        if (email!.Length > EmailMaxLength)
        {
            errors.Add($"Email exceeds {EmailMaxLength} characters.");
        }
        if (messageContent!.Length > MessageMaxLength)
        {
            errors.Add($"Message exceeds {MessageMaxLength} characters.");
        }

        if (errors.Count > 0)
        {
            _logger.LogWarning(
                "Form submission exceeded field limits. Errors: {Errors}. Name length: {NameLen}, Email length: {EmailLen}, Message length: {MessageLen}",
                string.Join(" | ", errors), name.Length, email.Length, messageContent.Length);

            var badRequestResponse = req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
            await badRequestResponse.WriteAsJsonAsync(new
            {
                title = "Bad Request",
                status = (int)System.Net.HttpStatusCode.BadRequest,
                detail = string.Join(" ", errors)
            }).ConfigureAwait(false);
            return new SendMessageOutput
            {
                HttpResponse = badRequestResponse,
                ContactRow = null
            };
        }

        // Return a redirect so the user sees the thank you page after submitting
        var response = req.CreateResponse(System.Net.HttpStatusCode.SeeOther);
        response.Headers.Add("Location", "/kontakt-erfolgreich");

        MessageEntity messageEntity = new(name, email, messageContent);

        // Return both the HTTP response and the entity to be written to Table Storage
        return new SendMessageOutput
        {
            HttpResponse = response,
            ContactRow = messageEntity
        };
    }
}
