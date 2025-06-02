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

public sealed class ReceiveMessageOutput
{
    public required HttpResponseData HttpResponse { get; init; }

    [TableOutput("Contacts", Connection = "AzureWebJobsStorage")]
    public required MessageEntity ContactRow { get; init; }
}

public class ReceiveMessageFunction(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<ReceiveMessageFunction>();

    [Function("ReceiveMessage")]
    public async Task<ReceiveMessageOutput> Run(
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

        // Basic validation for required fields
        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(messageContent))
        {
            _logger.LogWarning("Form submission contained missing fields. Name: '{Name}', Email: '{Email}', Message: '{Message}'", name, email, messageContent);
            // Throwing an exception will result in a 500 Internal Server Error.
            // For a production scenario, a more graceful error response (e.g., HTTP 400) should be crafted.
            throw new ArgumentException("Name, Email, and Message are required fields and must be provided.");
        }

        // Create the HTTP 200 OK response with a simple "ok" payload
        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteStringAsync("ok").ConfigureAwait(false);

        MessageEntity messageEntity = new(name, email, messageContent);

        // Return both the HTTP response and the entity to be written to Table Storage
        return new ReceiveMessageOutput
        {
            HttpResponse = response,
            ContactRow = messageEntity
        };
    }
}
