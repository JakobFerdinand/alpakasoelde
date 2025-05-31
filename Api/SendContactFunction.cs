using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Azure;
using Azure.Data.Tables;
using Azure.Identity;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Api;

public class SendContactFunction
{
    private readonly ILogger _logger;

    public SendContactFunction(ILoggerFactory loggerFactory)
        => _logger = loggerFactory.CreateLogger<SendContactFunction>();

    private record ContactInput(string Name, string Email, string Message);

    private class ContactEntity : ITableEntity
    {
        public string PartitionKey { get; init; } = "messages";
        public string RowKey { get; init; } = Guid.NewGuid().ToString();
        public string? Name { get; init; }
        public string? Email { get; init; }
        public string? Message { get; init; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; } = ETag.All;
    }

    [Function("SendContact")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        // Read request body
        using var reader = new StreamReader(req.Body);
        var body = await reader.ReadToEndAsync();
        _logger.LogInformation("Received data: {Body}", body);

        // Deserialize input
        var input = JsonSerializer.Deserialize<ContactInput>(body);
        if (input is ContactInput { Name: var name, Email: var email, Message: var message })
        {
            var tableUrl = Environment.GetEnvironmentVariable("MESSAGES_TABLE");
            if (!string.IsNullOrWhiteSpace(tableUrl))
            {
                // Create TableClient with TokenCredential and default options
                TableClient client = new(
                    new Uri(tableUrl),
                    new DefaultAzureCredential(),
                    new TableClientOptions()
                );

                await client.CreateIfNotExistsAsync();

                ContactEntity entity = new()
                {
                    Name = name,
                    Email = email,
                    Message = message
                };

                await client.AddEntityAsync(entity);
            }
            else
            {
                _logger.LogError("MESSAGES_TABLE environment variable not set");
            }
        }
        else
        {
            _logger.LogError("Invalid request body");
        }

        var response = req.CreateResponse(System.Net.HttpStatusCode.OK);
        await response.WriteStringAsync("ok");
        return response;
    }
}
