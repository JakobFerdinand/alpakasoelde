using System.Net;
using System.Text.Json;
using Azure.Data.Tables;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using website_api.shared.entities;

namespace WebsiteApi.Features.PageViews;

public sealed class PageView
{
	private readonly Handler _handler;
	private readonly ILogger<PageView> _logger;

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public PageView(Handler handler, ILogger<PageView> logger)
	{
		_handler = handler;
		_logger = logger;
	}

	[Function("pageview")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req,
		FunctionContext context)
	{
		string? body = await new StreamReader(req.Body).ReadToEndAsync().ConfigureAwait(false);

		Payload? payload;
		try
		{
			payload = JsonSerializer.Deserialize<Payload>(body, JsonOptions);
		}
		catch (JsonException)
		{
			payload = null;
		}

		if (payload is null)
		{
			var invalidJsonResponse = req.CreateResponse(HttpStatusCode.BadRequest);
			await invalidJsonResponse.WriteAsJsonAsync(new
			{
				title = "Bad Request",
				status = (int)HttpStatusCode.BadRequest,
				detail = "Invalid JSON payload."
			}).ConfigureAwait(false);
			return invalidJsonResponse;
		}

		var command = new Command(payload.Path ?? string.Empty, payload.ReferrerHost, payload.ViewportWidth ?? 0);
		var validation = await _handler.HandleAsync(command, req.FunctionContext.CancellationToken).ConfigureAwait(false);
		if (validation is not null)
		{
			_logger.LogWarning("Pageview validation failed: {Detail}", validation.Detail);
			var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
			await badRequestResponse.WriteAsJsonAsync(new
			{
				title = "Bad Request",
				status = (int)HttpStatusCode.BadRequest,
				detail = validation.Detail
			}).ConfigureAwait(false);
			return badRequestResponse;
		}

		return req.CreateResponse(HttpStatusCode.NoContent);
	}

	public sealed record Payload(string? Path, string? ReferrerHost, int? ViewportWidth);

	public sealed record Command(string Path, string? ReferrerHost, int ViewportWidth);

	public sealed record ValidationProblem(IReadOnlyCollection<string> Errors, string Detail);

	public interface IPageViewWriteStore
	{
		Task AddAsync(PageViewEntity entity, CancellationToken cancellationToken);
	}

	public sealed class TablePageViewStore(TableServiceClient tableServiceClient) : IPageViewWriteStore
	{
		private readonly TableServiceClient _tableServiceClient = tableServiceClient;

		public async Task AddAsync(PageViewEntity entity, CancellationToken cancellationToken)
		{
			TableClient tableClient = _tableServiceClient.GetTableClient("pageviews");
			await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			await tableClient.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
		}
	}

	public sealed class Handler(IPageViewWriteStore store)
	{
		private readonly IPageViewWriteStore _store = store;

		private const int PathMaxLength = 200;
		private const int ReferrerHostMaxLength = 200;
		private const int MaxViewportWidth = 10000;

		public async Task<ValidationProblem?> HandleAsync(Command command, CancellationToken cancellationToken)
		{
			List<string> missingFields = [];
			if (string.IsNullOrWhiteSpace(command.Path)) missingFields.Add("Path");

			if (missingFields.Count > 0)
			{
				return new ValidationProblem(missingFields, $"{string.Join(", ", missingFields)} are required fields and must be provided.");
			}

			List<string> errors = [];
			if (command.Path.Length > PathMaxLength) errors.Add($"Path exceeds {PathMaxLength} characters.");
			if (!command.Path.StartsWith('/')) errors.Add("Path must start with '/'.");
			if (!string.IsNullOrEmpty(command.ReferrerHost) && command.ReferrerHost.Length > ReferrerHostMaxLength) errors.Add($"ReferrerHost exceeds {ReferrerHostMaxLength} characters.");
			if (command.ViewportWidth is < 0 or > MaxViewportWidth) errors.Add($"ViewportWidth must be between 0 and {MaxViewportWidth}.");

			if (errors.Count > 0)
			{
				return new ValidationProblem(errors, string.Join(" ", errors));
			}

			PageViewEntity entity = new()
			{
				PartitionKey = $"Pv|{DateTime.UtcNow:yyyy-MM-dd}",
				RowKey = Guid.NewGuid().ToString(),
				Path = command.Path.Trim(),
				ReferrerHost = string.IsNullOrEmpty(command.ReferrerHost) ? null : command.ReferrerHost.Trim(),
				ViewportWidth = command.ViewportWidth,
			};

			await _store.AddAsync(entity, cancellationToken).ConfigureAwait(false);

			return null;
		}
	}
}