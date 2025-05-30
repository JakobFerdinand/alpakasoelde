using System.IO;
using System.Threading.Tasks;
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

        [Function("SendContact")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            var body = await new StreamReader(req.Body).ReadToEndAsync();
            _logger.LogInformation("Received data: {Body}", body);

            var res = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await res.WriteStringAsync("ok");
            return res;
        }
    }
}
