using System.Net;
using System.Text.Json;
using Azure;
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

		var command = new Command(payload.Path ?? string.Empty, payload.ReferrerHost, payload.ViewportWidth ?? 0, payload.SessionId, payload.VisitorId, payload.NavigationType);
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

	public sealed record Payload(string? Path, string? ReferrerHost, int? ViewportWidth, string? SessionId, string? VisitorId, string? NavigationType);

	public sealed record Command(string Path, string? ReferrerHost, int ViewportWidth, string? SessionId, string? VisitorId, string? NavigationType);

	public sealed record ValidationProblem(IReadOnlyCollection<string> Errors, string Detail);

	public interface IPageViewWriteStore
	{
		Task AddAsync(PageViewEntity entity, CancellationToken cancellationToken);
	}

	public sealed class TablePageViewStore(TableServiceClient tableServiceClient, ILogger<TablePageViewStore> logger) : IPageViewWriteStore
	{
		private const int RetentionMonths = 36;
		private const string CleanupMarkerPartition = "Cleanup";
		private const string CleanupMarkerRowKey = "last";
		private static readonly TimeSpan CleanupInterval = TimeSpan.FromDays(1);

		private readonly TableServiceClient _tableServiceClient = tableServiceClient;
		private readonly ILogger<TablePageViewStore> _logger = logger;

		public async Task AddAsync(PageViewEntity entity, CancellationToken cancellationToken)
		{
			TableClient tableClient = _tableServiceClient.GetTableClient("pageviews");
			await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
			await tableClient.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
			await MaybePurgeExpiredAsync(tableClient, cancellationToken).ConfigureAwait(false);
		}

		private async Task MaybePurgeExpiredAsync(TableClient tableClient, CancellationToken cancellationToken)
		{
			try
			{
				NullableResponse<TableEntity> markerResponse = await tableClient.GetEntityIfExistsAsync<TableEntity>(CleanupMarkerPartition, CleanupMarkerRowKey, cancellationToken: cancellationToken).ConfigureAwait(false);
				TableEntity? marker = markerResponse.HasValue ? markerResponse.Value : null;
				if (marker is not null && DateTimeOffset.UtcNow - marker.GetDateTimeOffset("LastCleanupUtc") < CleanupInterval)
				{
					return;
				}

				DateTimeOffset cutoff = DateTimeOffset.UtcNow.AddMonths(-RetentionMonths);
				string cutoffKey = $"Pv|{cutoff:yyyy-MM-dd}";

				List<PageViewEntity> batch = [];
				await foreach (PageViewEntity entity in tableClient.QueryAsync<PageViewEntity>(filter: $"PartitionKey lt '{cutoffKey}'", cancellationToken: cancellationToken).ConfigureAwait(false))
				{
					batch.Add(entity);
					if (batch.Count == 100)
					{
						await SubmitDeleteBatchAsync(tableClient, batch, cancellationToken).ConfigureAwait(false);
					}
				}

				if (batch.Count > 0)
				{
					await SubmitDeleteBatchAsync(tableClient, batch, cancellationToken).ConfigureAwait(false);
				}

				await tableClient.UpsertEntityAsync(new TableEntity(CleanupMarkerPartition, CleanupMarkerRowKey) { ["LastCleanupUtc"] = DateTimeOffset.UtcNow }, TableUpdateMode.Replace, cancellationToken: cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogWarning("Pageview cleanup failed: {Error}", ex.Message);
			}
		}

		private static async Task SubmitDeleteBatchAsync(TableClient tableClient, List<PageViewEntity> batch, CancellationToken cancellationToken)
		{
			await tableClient.SubmitTransactionAsync([.. batch.Select(e => new TableTransactionAction(TableTransactionActionType.Delete, e))], cancellationToken).ConfigureAwait(false);
			batch.Clear();
		}
	}

	public sealed class Handler(IPageViewWriteStore store)
	{
		private readonly IPageViewWriteStore _store = store;

		private const int PathMaxLength = 200;
		private const int ReferrerHostMaxLength = 200;
		private const int MaxViewportWidth = 10000;
		private const int IdMaxLength = 64;
		private static readonly string[] AllowedNavigationTypes = ["navigate", "reload", "back_forward"];

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

			string path = command.Path.Trim().TrimEnd('/');
			if (path.Length == 0)
			{
				path = "/";
			}

			PageViewEntity entity = new()
			{
				PartitionKey = $"Pv|{DateTime.UtcNow:yyyy-MM-dd}",
				RowKey = Guid.NewGuid().ToString(),
				Path = path,
				ReferrerHost = string.IsNullOrEmpty(command.ReferrerHost) ? null : command.ReferrerHost.Trim(),
				ViewportWidth = command.ViewportWidth,
				SessionId = SanitizeIdentifier(command.SessionId),
				VisitorId = SanitizeIdentifier(command.VisitorId),
				NavigationType = SanitizeNavigationType(command.NavigationType),
			};

			await _store.AddAsync(entity, cancellationToken).ConfigureAwait(false);

			return null;
		}

		private static string? SanitizeIdentifier(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return null;
			}

			string trimmed = value.Trim();
			return trimmed.Length > IdMaxLength ? null : trimmed;
		}

		private static string? SanitizeNavigationType(string? value)
		{
			if (string.IsNullOrWhiteSpace(value))
			{
				return null;
			}

			string trimmed = value.Trim();
			return AllowedNavigationTypes.Contains(trimmed, StringComparer.Ordinal) ? trimmed : null;
		}
	}
}