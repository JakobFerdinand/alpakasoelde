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

namespace Api
{
    public class SendContactFunction
    {
        private readonly ILogger _logger;

        public SendContactFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<SendContactFunction>();
        }

        private record ContactInput(string Name, string Email, string Message);

        private class ContactEntity : ITableEntity
        {
            public string PartitionKey { get; set; } = "messages";
            public string RowKey { get; set; } = Guid.NewGuid().ToString();
            public string? Name { get; set; }
            public string? Email { get; set; }
            public string? Message { get; set; }
            public DateTimeOffset? Timestamp { get; set; }
            public ETag ETag { get; set; } = ETag.All;
        }

        [Function("SendContact")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Received data: {Body}", body);

            ContactInput? input = JsonSerializer.Deserialize<ContactInput>(body);
            if (input is not null)
            {
                var tableUrl = Environment.GetEnvironmentVariable("MESSAGES_TABLE");
                if (!string.IsNullOrEmpty(tableUrl))
                {
                    var client = new TableClient(new Uri(tableUrl), new DefaultAzureCredential());
                    await client.CreateIfNotExistsAsync();
                    var entity = new ContactEntity
                    {
                        Name = input.Name,
                        Email = input.Email,
                        Message = input.Message
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

            var res = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await res.WriteStringAsync("ok");
            return res;
        }
    }
}
