using System.Net;
using System.Linq;
using Azure;
using Azure.Data.Tables;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using IImageUrlSigner = DashboardApi.Features.Alpakas.GetAlpakas.IImageUrlSigner;

namespace DashboardApi.Features.Alpakas;

public sealed class GetAlpakaById
{
	private readonly Handler _handler;
	private readonly ILogger<GetAlpakaById> _logger;

	public GetAlpakaById(Handler handler, ILogger<GetAlpakaById> logger)
	{
		_handler = handler;
		_logger = logger;
	}

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

		Result? alpaka = await _handler.HandleAsync(new Query(alpakaId), req.FunctionContext.CancellationToken);
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

	public sealed record Query(string Id);

        public sealed record Result(string Id, string Name, string Geburtsdatum, string? ImageUrl, IReadOnlyList<EventResult> Events);

        public sealed record EventResult(string Id, string EventType, string EventDate, string? Comment, double? Cost);

        public interface IReadStore
        {
                Task<AlpakaEntity?> GetByIdAsync(string id, CancellationToken cancellationToken);
        }

        public interface IEventReadStore
        {
                Task<IReadOnlyList<EventEntity>> GetByAlpakaIdAsync(string alpakaId, CancellationToken cancellationToken);
        }

	public sealed class TableReadStore(TableServiceClient tableServiceClient) : IReadStore
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

        public sealed class Handler(IReadStore readStore, IImageUrlSigner imageSigner, IEventReadStore eventReadStore)
        {
                private readonly IReadStore _readStore = readStore;
                private readonly IImageUrlSigner _imageSigner = imageSigner;
                private readonly IEventReadStore _eventReadStore = eventReadStore;

                public async Task<Result?> HandleAsync(Query query, CancellationToken cancellationToken)
                {
                        AlpakaEntity? alpaka = await _readStore.GetByIdAsync(query.Id, cancellationToken).ConfigureAwait(false);
                        if (alpaka is null)
                        {
                                return null;
                        }

                        IReadOnlyList<EventEntity> events = await _eventReadStore
                                .GetByAlpakaIdAsync(query.Id, cancellationToken)
                                .ConfigureAwait(false);

                        List<EventResult> eventResults = events
                                .OrderByDescending(e => e.EventDate)
                                .ThenByDescending(e => e.RowKey)
                                .Select(e => new EventResult(
                                        e.RowKey,
                                        e.EventType,
                                        e.EventDate.ToString("yyyy-MM-dd"),
                                        e.Comment,
                                        e.Cost))
                                .ToList();

                        string? signedUrl = _imageSigner.TrySignReadUrl(alpaka.ImageUrl, TimeSpan.FromMinutes(30));
                        return new Result(alpaka.RowKey, alpaka.Name, alpaka.Geburtsdatum, signedUrl, eventResults);
                }
        }

        public sealed class TableEventReadStore(TableServiceClient tableServiceClient) : IEventReadStore
        {
                private readonly TableServiceClient _tableServiceClient = tableServiceClient;

                public async Task<IReadOnlyList<EventEntity>> GetByAlpakaIdAsync(string alpakaId, CancellationToken cancellationToken)
                {
                        TableClient tableClient = _tableServiceClient.GetTableClient("events");
                        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                        var events = tableClient
                                .Query<EventEntity>(e => e.PartitionKey == alpakaId)
                                .ToList();

                        return events;
                }
        }
}
