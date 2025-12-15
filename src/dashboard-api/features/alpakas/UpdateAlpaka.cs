using System.Net;
using Azure;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using dashboard_api.shared.entities;
using HttpMultipartParser;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using AlpakaImagePayload = DashboardApi.Features.Alpakas.AddAlpaka.AlpakaImagePayload;
using IImageUrlSigner = DashboardApi.Features.Alpakas.GetAlpakas.IImageUrlSigner;

namespace DashboardApi.Features.Alpakas;

public sealed class UpdateAlpaka
{
	private readonly Handler _handler;
	private readonly ILogger<UpdateAlpaka> _logger;

	public UpdateAlpaka(Handler handler, ILogger<UpdateAlpaka> logger)
	{
		_handler = handler;
		_logger = logger;
	}

	[Function("update-alpaka")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Function, "put", Route = "alpakas/{alpakaId}")] HttpRequestData req,
		string alpakaId)
	{
		if (string.IsNullOrWhiteSpace(alpakaId))
		{
			var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
			await badRequest.WriteStringAsync("Alpaka id is required.").ConfigureAwait(false);
			return badRequest;
		}

		var parsedFormBody = await MultipartFormDataParser.ParseAsync(req.Body).ConfigureAwait(false);
		string? name = parsedFormBody.GetParameterValue("name")?.Trim();
		string? geburtsdatum = parsedFormBody.GetParameterValue("geburtsdatum")?.Trim();
		FilePart? imageFile = parsedFormBody.Files.FirstOrDefault();

		AlpakaImagePayload? imagePayload = imageFile is { Data.Length: > 0 }
			? new AlpakaImagePayload(imageFile.Data, imageFile.FileName, imageFile.ContentType)
			: null;

		Response response = await _handler.HandleAsync(
			new Command(alpakaId, name ?? string.Empty, geburtsdatum ?? string.Empty, imagePayload),
			req.FunctionContext.CancellationToken);

		if (response.NotFound)
		{
			var notFound = req.CreateResponse(HttpStatusCode.NotFound);
			await notFound.WriteStringAsync("Alpaka not found.").ConfigureAwait(false);
			return notFound;
		}

		if (!response.IsValid)
		{
			var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
			await badRequest.WriteAsJsonAsync(new
			{
				title = "Bad Request",
				status = (int)HttpStatusCode.BadRequest,
				detail = string.Join(" ", response.ValidationErrors!)
			}).ConfigureAwait(false);
			return badRequest;
		}

		var result = response.Result!;
		var ok = req.CreateResponse(HttpStatusCode.OK);
		await ok.WriteAsJsonAsync(result).ConfigureAwait(false);
		return ok;
	}

	public sealed record Command(string Id, string Name, string Geburtsdatum, AlpakaImagePayload? Image);

	public sealed record Result(string Id, string Name, string Geburtsdatum, string? ImageUrl);

	public sealed record Response(Result? Result = null, IReadOnlyCollection<string>? ValidationErrors = null, bool NotFound = false)
	{
		public bool IsValid => ValidationErrors is null || ValidationErrors.Count == 0;
	}

	public interface IAlpakaUpdateStore
	{
		Task<AlpakaEntity?> GetAsync(string id, CancellationToken cancellationToken);
		Task UpdateAsync(AlpakaEntity entity, ETag etag, CancellationToken cancellationToken);
	}

	public interface IAlpakaImageReplacementStore
	{
		Task<string?> ReplaceAsync(string? existingUrl, AlpakaImagePayload newImage, CancellationToken cancellationToken);
	}

	public sealed class TableAlpakaUpdateStore(TableServiceClient tableServiceClient) : IAlpakaUpdateStore
	{
		private readonly TableServiceClient _tableServiceClient = tableServiceClient;

		public async Task<AlpakaEntity?> GetAsync(string id, CancellationToken cancellationToken)
		{
			TableClient tableClient = _tableServiceClient.GetTableClient("alpakas");
			try
			{
				var response = await tableClient.GetEntityAsync<AlpakaEntity>("AlpakaPartition", id, cancellationToken: cancellationToken).ConfigureAwait(false);
				return response.Value;
			}
			catch (RequestFailedException ex) when (ex.Status == 404)
			{
				return null;
			}
		}

		public Task UpdateAsync(AlpakaEntity entity, ETag etag, CancellationToken cancellationToken)
		{
			TableClient tableClient = _tableServiceClient.GetTableClient("alpakas");
			return tableClient.UpdateEntityAsync(entity, etag, TableUpdateMode.Replace, cancellationToken);
		}
	}

	public sealed class BlobAlpakaImageReplacementStore(BlobServiceClient blobServiceClient, ILogger<BlobAlpakaImageReplacementStore> logger) : IAlpakaImageReplacementStore
	{
		private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
		private readonly ILogger<BlobAlpakaImageReplacementStore> _logger = logger;

		private const long MaxImageSizeBytes = 15 * 1024 * 1024;
		private static readonly HashSet<string> AllowedExtensions = [".png", ".jpg", ".jpeg"];

		public async Task<string?> ReplaceAsync(string? existingUrl, AlpakaImagePayload newImage, CancellationToken cancellationToken)
		{
			if (newImage.Content.Length > MaxImageSizeBytes)
			{
				throw new InvalidOperationException($"Image file exceeds the maximum allowed size of {MaxImageSizeBytes / (1024 * 1024)}MB.");
			}

			if (!AllowedExtensions.Contains(newImage.Extension))
			{
				throw new InvalidOperationException("Unsupported image file type. Only .png, .jpg or .jpeg is allowed.");
			}

			BlobContainerClient container = _blobServiceClient.GetBlobContainerClient("alpakas");
			await container.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

			string newBlobName = $"{Guid.NewGuid()}{newImage.Extension}";
			BlobClient newBlob = container.GetBlobClient(newBlobName);
			await newBlob.UploadAsync(newImage.Content, new Azure.Storage.Blobs.Models.BlobHttpHeaders { ContentType = newImage.ContentType }, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			if (!string.IsNullOrWhiteSpace(existingUrl))
			{
				string existingBlobName = Path.GetFileName(new Uri(existingUrl).AbsolutePath);
				BlobClient oldBlob = container.GetBlobClient(existingBlobName);
				try
				{
					await oldBlob.DeleteIfExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
				}
				catch (RequestFailedException ex)
				{
					_logger.LogWarning(ex, "Failed to delete old alpaka image blob {BlobName}.", existingBlobName);
				}
			}

			return newBlob.Uri.ToString();
		}
	}

	public sealed class Handler(IAlpakaUpdateStore alpakaStore, IAlpakaImageReplacementStore imageStore, ILogger<Handler> logger, IImageUrlSigner imageSigner)
	{
		private readonly IAlpakaUpdateStore _alpakaStore = alpakaStore;
		private readonly IAlpakaImageReplacementStore _imageStore = imageStore;
		private readonly ILogger<Handler> _logger = logger;
		private readonly IImageUrlSigner _imageSigner = imageSigner;

		private const int NameMaxLength = 100;

		public async Task<Response> HandleAsync(Command command, CancellationToken cancellationToken)
		{
			List<string> errors = [];
			if (string.IsNullOrWhiteSpace(command.Name))
			{
				errors.Add("Name");
			}
			if (string.IsNullOrWhiteSpace(command.Geburtsdatum))
			{
				errors.Add("Geburtsdatum");
			}
			if (errors.Count > 0)
			{
				return new Response(ValidationErrors: errors);
			}

			if (command.Name.Length > NameMaxLength)
			{
				return new Response(ValidationErrors: [$"Name exceeds {NameMaxLength} characters."]);
			}

			AlpakaEntity? existing = await _alpakaStore.GetAsync(command.Id, cancellationToken).ConfigureAwait(false);
			if (existing is null)
			{
				return new Response(NotFound: true);
			}

			existing.Name = command.Name.Trim();
			existing.Geburtsdatum = command.Geburtsdatum.Trim();

			if (command.Image is not null)
			{
				try
				{
					existing.ImageUrl = await _imageStore.ReplaceAsync(existing.ImageUrl, command.Image, cancellationToken).ConfigureAwait(false);
				}
				catch (InvalidOperationException ex)
				{
					return new Response(ValidationErrors: [ex.Message]);
				}
			}

			await _alpakaStore.UpdateAsync(existing, existing.ETag, cancellationToken).ConfigureAwait(false);
			string? signedUrl = _imageSigner.TrySignReadUrl(existing.ImageUrl, TimeSpan.FromMinutes(30));
			return new Response(new Result(existing.RowKey, existing.Name, existing.Geburtsdatum, signedUrl));
		}
	}
}
