using Azure;
using Azure.Data.Tables;
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

        var response = req.CreateResponse(System.Net.HttpStatusCode.SeeOther);
        response.Headers.Add("Location", "/nachricht-gesendet");

        MessageEntity messageEntity = new(name!, email!, messageContent!);

        string? connectionString = Environment.GetEnvironmentVariable("MessageStorageConnection");
        var tableClient = new TableClient(connectionString, "messages");
        await tableClient.AddEntityAsync(messageEntity).ConfigureAwait(false);

        return response;
    }
}
