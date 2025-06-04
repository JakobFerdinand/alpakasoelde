using Azure;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.IO;
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
        string? name = parsedForm["name"];
        string? email = parsedForm["email"];
        string? messageContent = parsedForm["message"]; // Use messageContent to avoid potential naming conflicts

        _logger.LogInformation("Parsed form data - Name: '{Name}', Email: '{Email}', Message: '{Message}'", name, email, messageContent);

        // Basic validation for required fields
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(messageContent))
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

        // Return a redirect so the user sees the thank you page after submitting
        var response = req.CreateResponse(System.Net.HttpStatusCode.SeeOther);
        response.Headers.Add("Location", "/nachricht-gesendet");

        MessageEntity messageEntity = new(name, email, messageContent);

        // Return both the HTTP response and the entity to be written to Table Storage
        return new SendMessageOutput
        {
            HttpResponse = response,
            ContactRow = messageEntity
        };
    }
}
