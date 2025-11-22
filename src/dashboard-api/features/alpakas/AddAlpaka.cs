using System.Net;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using dashboard_api.shared.entities;
using HttpMultipartParser;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Alpakas;

public sealed class AddAlpaka
{
	private readonly Handler _handler;
	private readonly ILogger<AddAlpaka> _logger;

	public AddAlpaka(Handler handler, ILogger<AddAlpaka> logger)
	{
		_handler = handler;
		_logger = logger;
	}

	[Function("add-alpaka")]
	public async Task<HttpResponseData> Run(
		[HttpTrigger(AuthorizationLevel.Function, "post", Route = "alpakas")] HttpRequestData req)
	{
		var parsedFormBody = await HttpMultipartParser.MultipartFormDataParser.ParseAsync(req.Body).ConfigureAwait(false);

		string? name = parsedFormBody.GetParameterValue("name")?.Trim();
		string? geburtsdatum = parsedFormBody.GetParameterValue("geburtsdatum")?.Trim();
		var imageFile = parsedFormBody.Files.FirstOrDefault();

		AlpakaImagePayload? imagePayload = imageFile is { Data.Length: > 0 }
			? new AlpakaImagePayload(imageFile.Data, imageFile.FileName, imageFile.ContentType)
			: null;

		Response response = await _handler.HandleAsync(
			new Command(name ?? string.Empty, geburtsdatum ?? string.Empty, imagePayload),
			req.FunctionContext.CancellationToken);

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
		var seeOther = req.CreateResponse(HttpStatusCode.SeeOther);
		seeOther.Headers.Add("Location", "/");
		await seeOther.WriteAsJsonAsync(result).ConfigureAwait(false);
		return seeOther;
	}

	public sealed record Command(string Name, string Geburtsdatum, AlpakaImagePayload? Image);

	public sealed record AlpakaImagePayload(Stream Content, string FileName, string ContentType)
	{
		public string Extension => Path.GetExtension(FileName).ToLowerInvariant();
	}

	public sealed record Result(string Id, string Name, string Geburtsdatum, string? ImageUrl);

	public sealed record Response(Result? Result = null, IReadOnlyCollection<string>? ValidationErrors = null)
	{
		public bool IsValid => ValidationErrors is null || ValidationErrors.Count == 0;
	}

	public interface IAlpakaWriteStore
	{
		Task AddAsync(AlpakaEntity entity, CancellationToken cancellationToken);
	}

	public interface IAlpakaImageStore
	{
		Task<string?> UploadAsync(AlpakaImagePayload image, CancellationToken cancellationToken);
	}

	public sealed class TableAlpakaWriteStore(TableServiceClient tableServiceClient) : IAlpakaWriteStore
	{
		private readonly TableServiceClient _tableServiceClient = tableServiceClient;

		public async Task AddAsync(AlpakaEntity entity, CancellationToken cancellationToken)
		{
			TableClient tableClient = _tableServiceClient.GetTableClient("alpakas");
			await tableClient.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
		}
	}

	public sealed class BlobAlpakaImageStore(BlobServiceClient blobServiceClient, ILogger<BlobAlpakaImageStore> logger) : IAlpakaImageStore
	{
		private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
		private readonly ILogger<BlobAlpakaImageStore> _logger = logger;

		private static readonly HashSet<string> AllowedExtensions = [".png", ".jpg", ".jpeg"];

		public async Task<string?> UploadAsync(AlpakaImagePayload image, CancellationToken cancellationToken)
		{
			if (!AllowedExtensions.Contains(image.Extension))
			{
				_logger.LogWarning("Unsupported image file type: {Extension}", image.Extension);
				return null;
			}

			BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient("alpakas");
			await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

			string blobName = $"{Guid.NewGuid()}{image.Extension}";
			BlobClient blobClient = containerClient.GetBlobClient(blobName);
			await blobClient.UploadAsync(image.Content, new BlobHttpHeaders { ContentType = image.ContentType }, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			return blobClient.Uri.ToString();
		}
	}

	public sealed class Handler(IAlpakaWriteStore writeStore, IAlpakaImageStore imageStore, ILogger<Handler> logger)
	{
		private readonly IAlpakaWriteStore _writeStore = writeStore;
		private readonly IAlpakaImageStore _imageStore = imageStore;
		private readonly ILogger<Handler> _logger = logger;

		private const int NameMaxLength = 100;
		private const long MaxImageSizeBytes = 15 * 1024 * 1024;

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
				return new Response(ValidationErrors: [
					$"Name exceeds {NameMaxLength} characters."
				]);
			}

			string? imageUrl = null;
			if (command.Image is { })
			{
				if (command.Image.Content.Length > MaxImageSizeBytes)
				{
					return new Response(ValidationErrors: [
						$"Image file exceeds the maximum allowed size of {MaxImageSizeBytes / (1024 * 1024)}MB."
					]);
				}

				imageUrl = await _imageStore.UploadAsync(command.Image, cancellationToken).ConfigureAwait(false);
			}

			AlpakaEntity entity = new()
			{
				Name = command.Name.Trim(),
				Geburtsdatum = command.Geburtsdatum.Trim(),
				ImageUrl = imageUrl
			};

			await _writeStore.AddAsync(entity, cancellationToken).ConfigureAwait(false);
			_logger.LogInformation("Alpaka {Name} added with id {Id}", entity.Name, entity.RowKey);
			return new Response(new Result(entity.RowKey, entity.Name, entity.Geburtsdatum, entity.ImageUrl));
		}
	}
}
