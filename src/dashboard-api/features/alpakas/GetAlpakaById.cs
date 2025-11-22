using System.Net;
using Azure;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Alpakas;

public sealed record GetAlpakaByIdQuery(string Id);

public sealed record AlpakaDetailResult(string Id, string Name, string Geburtsdatum, string? ImageUrl);

public interface IAlpakaByIdReadStore
{
	Task<AlpakaEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);
}

public sealed class TableAlpakaByIdReadStore(TableServiceClient tableServiceClient) : IAlpakaByIdReadStore
{
	private readonly TableServiceClient _tableServiceClient = tableServiceClient;

	public async Task<AlpakaEntity?> GetByIdAsync(string id, CancellationToken cancellationToken)
	{
		TableClient tableClient = _tableServiceClient.GetTableClient("alpakas");
		try
		{
			var entityResponse = await tableClient.GetEntityAsync<AlpakaEntity>("AlpakaPartition", id, cancellationToken: cancellationToken)
				.ConfigureAwait(false);
			return entityResponse.Value;
		}
		catch (RequestFailedException ex) when (ex.Status == 404)
		{
			return null;
		}
	}
}

public sealed class GetAlpakaByIdHandler(IAlpakaByIdReadStore readStore, IImageUrlSigner imageSigner)
{
	private readonly IAlpakaByIdReadStore _readStore = readStore;
	private readonly IImageUrlSigner _imageSigner = imageSigner;

	public async Task<AlpakaDetailResult?> HandleAsync(GetAlpakaByIdQuery query, CancellationToken cancellationToken)
	{
		AlpakaEntity? alpaka = await _readStore.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
		if (alpaka is null)
		{
			return null;
		}

		string? signedUrl = _imageSigner.TrySignReadUrl(alpaka.ImageUrl, TimeSpan.FromMinutes(30));
		return new AlpakaDetailResult(alpaka.RowKey, alpaka.Name, alpaka.Geburtsdatum, signedUrl);
	}
}

public sealed class GetAlpakaByIdFunction(GetAlpakaByIdHandler handler, ILogger<GetAlpakaByIdFunction> logger)
{
	private readonly GetAlpakaByIdHandler _handler = handler;
	private readonly ILogger<GetAlpakaByIdFunction> _logger = logger;

	[Function("get-alpaka-by-id")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Function, "get", Route = "alpakas/{alpakaId}")] HttpRequestData req,
		string alpakaId)
	{
		if (string.IsNullOrWhiteSpace(alpakaId))
		{
			var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
			await badRequest.WriteStringAsync("Alpaka id is required.").ConfigureAwait(false);
			return badRequest;
		}

		AlpakaDetailResult? alpaka = await _handler.HandleAsync(new GetAlpakaByIdQuery(alpakaId), req.FunctionContext.CancellationToken);
		if (alpaka is null)
		{
			var notFound = req.CreateResponse(HttpStatusCode.NotFound);
			await notFound.WriteStringAsync("Alpaka not found.").ConfigureAwait(false);
			return notFound;
		}

		var response = req.CreateResponse(HttpStatusCode.OK);
		await response.WriteAsJsonAsync(alpaka).ConfigureAwait(false);
		return response;
	}
}
