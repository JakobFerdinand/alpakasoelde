using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace Api;

public class GetAlpakasFunction(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<GetAlpakasFunction>();

    [Function("get-alpakas")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "dashboard/alpakas")] HttpRequestData req)
    {
        string? connectionString = Environment.GetEnvironmentVariable(EnvironmentVariables.StorageConnection);
        TableClient tableClient = new(connectionString, "alpakas");

        var alpakas = tableClient
            .Query<AlpakaEntity>()
            .OrderBy(a => a.Name)
            .Select(a => new
            {
                Id = a.RowKey,
                a.Name,
                a.Geburtsdatum,
                ImageUrl = a.ImageUrl
            })
            .ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(alpakas).ConfigureAwait(false);
        return response;
    }
}
