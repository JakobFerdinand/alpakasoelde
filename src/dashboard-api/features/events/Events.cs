using System.Net;
using System.Net.Http;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using DashboardApi.Features.Alpakas;
using dashboard_api.shared.entities;

namespace DashboardApi.Features.Events;

public sealed record GetEventsQuery;
public sealed record AddEventCommand(string EventType, List<string> AlpakaIds, string EventDate, double? Cost, string? Comment);
public sealed record EventResult(string Id, string EventType, string EventDate, string? Comment, double? Cost, List<string> AlpakaIds, List<string> AlpakaNames);
public sealed record AddEventResult(string Id);

public interface IEventStore
{
	Task<IReadOnlyList<EventEntity>> GetAllAsync(CancellationToken cancellationToken);
	Task AddAsync(EventEntity entity, CancellationToken cancellationToken);
}

public interface IAlpakaLookupStore
{
	Task<IDictionary<string, string>> GetNamesAsync(CancellationToken cancellationToken);
}

public sealed class TableEventStore(TableServiceClient tableServiceClient) : IEventStore
{
	private readonly TableServiceClient _tableServiceClient = tableServiceClient;

	public Task<IReadOnlyList<EventEntity>> GetAllAsync(CancellationToken cancellationToken)
	{
		TableClient eventsTableClient = _tableServiceClient.GetTableClient("events");
		var events = eventsTableClient
			.Query<EventEntity>()
			.ToList();
		return Task.FromResult<IReadOnlyList<EventEntity>>(events);
	}

	public async Task AddAsync(EventEntity entity, CancellationToken cancellationToken)
	{
		TableClient eventsTableClient = _tableServiceClient.GetTableClient("events");
		await eventsTableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
		await eventsTableClient.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
	}
}

public sealed class TableAlpakaLookupStore(TableServiceClient tableServiceClient) : IAlpakaLookupStore
{
	private readonly TableServiceClient _tableServiceClient = tableServiceClient;

	public Task<IDictionary<string, string>> GetNamesAsync(CancellationToken cancellationToken)
	{
		TableClient alpakaTableClient = _tableServiceClient.GetTableClient("alpakas");
		var lookup = alpakaTableClient
			.Query<AlpakaEntity>()
			.ToDictionary(a => a.RowKey, a => a.Name, StringComparer.OrdinalIgnoreCase);
		return Task.FromResult<IDictionary<string, string>>(lookup);
	}
}

public sealed class GetEventsHandler(IEventStore eventStore, IAlpakaLookupStore alpakaLookup)
{
	private readonly IEventStore _eventStore = eventStore;
	private readonly IAlpakaLookupStore _alpakaLookup = alpakaLookup;

	public async Task<IReadOnlyList<EventResult>> HandleAsync(GetEventsQuery query, CancellationToken cancellationToken)
	{
		IReadOnlyList<EventEntity> events = await _eventStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
		IDictionary<string, string> alpakaLookup = await _alpakaLookup.GetNamesAsync(cancellationToken).ConfigureAwait(false);

		return events
			.GroupBy(e => string.IsNullOrWhiteSpace(e.SharedEventId) ? e.RowKey : e.SharedEventId)
			.Select(group =>
			{
				EventEntity first = group.First();
				List<string> alpakaIds = group
					.Select(e => e.PartitionKey)
					.Where(id => !string.IsNullOrWhiteSpace(id))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();

				List<string> alpakaNames = alpakaIds
					.Select(id => alpakaLookup.TryGetValue(id, out string? name) ? name : null)
					.Where(name => !string.IsNullOrWhiteSpace(name))
					.Cast<string>()
					.ToList();

				return new EventResult(
					string.IsNullOrWhiteSpace(first.SharedEventId) ? first.RowKey : first.SharedEventId,
					first.EventType,
					first.EventDate.ToString("yyyy-MM-dd"),
					first.Comment,
					first.Cost,
					alpakaIds,
					alpakaNames);
			})
			.OrderByDescending(e => e.EventDate)
			.ThenByDescending(e => e.Id)
			.ToList();
	}
}

public sealed class AddEventHandler(IEventStore eventStore, ILogger<AddEventHandler> logger)
{
	private readonly IEventStore _eventStore = eventStore;
	private readonly ILogger<AddEventHandler> _logger = logger;

	private const int MaxEventTypeLength = 100;
	private const int MaxCommentLength = 1000;

	public async Task<(AddEventResult? Result, string? Error)> HandleAsync(AddEventCommand command, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(command.EventType))
		{
			return (null, "Das Ereignisfeld ist erforderlich.");
		}

		if (command.EventType.Length > MaxEventTypeLength)
		{
			return (null, $"Der Ereignistyp darf maximal {MaxEventTypeLength} Zeichen lang sein.");
		}

		if (command.AlpakaIds.Count == 0)
		{
			return (null, "Mindestens ein Alpaka muss ausgewählt werden.");
		}

		if (command.Comment is { Length: > MaxCommentLength })
		{
			return (null, $"Die Notiz darf maximal {MaxCommentLength} Zeichen enthalten.");
		}

		if (!DateTimeOffset.TryParse(command.EventDate, out DateTimeOffset parsedDate))
		{
			return (null, "Das Datum ist ungültig.");
		}

		if (command.Cost is < 0)
		{
			return (null, "Kosten dürfen nicht negativ sein.");
		}

		string sharedEventId = Guid.NewGuid().ToString();
		foreach (string alpakaId in command.AlpakaIds)
		{
			EventEntity entity = new()
			{
				EventType = command.EventType.Trim(),
				EventDate = new DateTimeOffset(parsedDate.Date, TimeSpan.Zero),
				Comment = command.Comment?.Trim(),
				Cost = command.Cost,
				PartitionKey = alpakaId,
				RowKey = sharedEventId,
				SharedEventId = sharedEventId
			};

			await _eventStore.AddAsync(entity, cancellationToken).ConfigureAwait(false);
		}

		return (new AddEventResult(sharedEventId), null);
	}
}

public sealed class EventsFunction(GetEventsHandler getHandler, AddEventHandler addHandler, ILogger<EventsFunction> logger)
{
	private readonly GetEventsHandler _getHandler = getHandler;
	private readonly AddEventHandler _addHandler = addHandler;
	private readonly ILogger<EventsFunction> _logger = logger;

	[Function("events")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "events")] HttpRequestData req)
	{
		if (string.Equals(req.Method, HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase))
		{
			IReadOnlyList<EventResult> events = await _getHandler.HandleAsync(new GetEventsQuery(), req.FunctionContext.CancellationToken);
			var response = req.CreateResponse(HttpStatusCode.OK);
			await response.WriteAsJsonAsync(events).ConfigureAwait(false);
			return response;
		}

		if (!string.Equals(req.Method, HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase))
		{
			var methodNotAllowed = req.CreateResponse(HttpStatusCode.MethodNotAllowed);
			await methodNotAllowed.WriteAsJsonAsync(new
			{
				title = "Method Not Allowed",
				status = (int)HttpStatusCode.MethodNotAllowed
			}).ConfigureAwait(false);
			return methodNotAllowed;
		}

		AddEventRequest? payload;
		try
		{
			payload = await JsonSerializer.DeserializeAsync<AddEventRequest>(req.Body, new JsonSerializerOptions
			{
				PropertyNameCaseInsensitive = true
			}).ConfigureAwait(false);
		}
		catch (JsonException ex)
		{
			_logger.LogWarning(ex, "Invalid JSON payload for add-event.");
			var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
			await badRequest.WriteAsJsonAsync(new
			{
				title = "Bad Request",
				status = (int)HttpStatusCode.BadRequest,
				detail = "Ungültiger Anfrageinhalt."
			}).ConfigureAwait(false);
			return badRequest;
		}

		if (payload is null)
		{
			var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
			await badRequest.WriteAsJsonAsync(new
			{
				title = "Bad Request",
				status = (int)HttpStatusCode.BadRequest,
				detail = "Ein Ereignis muss angegeben werden."
			}).ConfigureAwait(false);
			return badRequest;
		}

		AddEventCommand command = new(
			payload.EventType ?? string.Empty,
			payload.AlpakaIds?.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList() ?? [],
			payload.EventDate ?? string.Empty,
			payload.Cost,
			payload.Comment?.Trim());

		var (result, error) = await _addHandler.HandleAsync(command, req.FunctionContext.CancellationToken);
		if (error is not null)
		{
			var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
			await badRequest.WriteAsJsonAsync(new
			{
				title = "Bad Request",
				status = (int)HttpStatusCode.BadRequest,
				detail = error
			}).ConfigureAwait(false);
			return badRequest;
		}

		var created = req.CreateResponse(HttpStatusCode.Created);
		await created.WriteAsJsonAsync(result).ConfigureAwait(false);
		return created;
	}
}

public sealed record AddEventRequest
{
	public string? EventType { get; init; }
	public List<string>? AlpakaIds { get; init; }
	public string? EventDate { get; init; }
	public double? Cost { get; init; }
	public string? Comment { get; init; }
}
