using System.Net;
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

public sealed class GetAlpakas
{
	public sealed class Function(Handler handler, ILogger<Function> logger)
	{
		private readonly Handler _handler = handler;
		private readonly ILogger<Function> _logger = logger;

		[Function("get-alpakas")]
		public async Task<HttpResponseData> Run(
			[HttpTrigger(AuthorizationLevel.Function, "get", Route = "alpakas")] HttpRequestData req)
		{
			IReadOnlyList<AlpakaListItem> alpakas = await _handler.HandleAsync(new Query(), req.FunctionContext.CancellationToken);
			var response = req.CreateResponse(HttpStatusCode.OK);
			await response.WriteAsJsonAsync(alpakas).ConfigureAwait(false);
			return response;
		}
	}

	public sealed record Query;

	public sealed record AlpakaListItem(string Id, string Name, string Geburtsdatum, string? ImageUrl);

	public interface IAlpakaReadStore
	{
		Task<IReadOnlyList<AlpakaEntity>> GetAllAsync(CancellationToken cancellationToken);
	}

	public interface IImageUrlSigner
	{
		string? TrySignReadUrl(string? originalUrl, TimeSpan lifetime);
	}

	public sealed class TableAlpakaReadStore(TableServiceClient tableServiceClient) : IAlpakaReadStore
	{
		private readonly TableServiceClient _tableServiceClient = tableServiceClient;

		public Task<IReadOnlyList<AlpakaEntity>> GetAllAsync(CancellationToken cancellationToken)
		{
			TableClient tableClient = _tableServiceClient.GetTableClient("alpakas");
			var entities = tableClient
				.Query<AlpakaEntity>()
				.OrderBy(a => a.Name)
				.ToList();
			return Task.FromResult<IReadOnlyList<AlpakaEntity>>(entities);
		}
	}

	public sealed class BlobImageUrlSigner(BlobServiceClient blobServiceClient, IConfiguration configuration) : IImageUrlSigner
	{
		private readonly BlobServiceClient _blobServiceClient = blobServiceClient;
		private readonly IConfiguration _configuration = configuration;

		public string? TrySignReadUrl(string? originalUrl, TimeSpan lifetime)
		{
			if (string.IsNullOrWhiteSpace(originalUrl))
			{
				return null;
			}

			string storageAccountName = _configuration[Shared.EnvironmentVariables.StorageAccountName]
				?? throw new InvalidOperationException("Environment variable 'AZURE_STORAGE_ACCOUNT_NAME' is not set.");
			string storageAccountKey = _configuration[Shared.EnvironmentVariables.StorageAccountKey]
				?? throw new InvalidOperationException("Environment variable 'AZURE_STORAGE_ACCOUNT_KEY' is not set.");

			string blobName = Path.GetFileName(new Uri(originalUrl).AbsolutePath);
			BlobContainerClient container = _blobServiceClient.GetBlobContainerClient("alpakas");
			BlobClient blob = container.GetBlobClient(blobName);

			var sasBuilder = new BlobSasBuilder
			{
				BlobContainerName = container.Name,
				BlobName = blobName,
				Resource = "b",
				ExpiresOn = DateTimeOffset.UtcNow.Add(lifetime)
			};
			sasBuilder.SetPermissions(BlobSasPermissions.Read);

			var credential = new StorageSharedKeyCredential(storageAccountName, storageAccountKey);
			var sasToken = sasBuilder.ToSasQueryParameters(credential).ToString();
			return $"{blob.Uri}?{sasToken}";
		}
	}

	public sealed class Handler(IAlpakaReadStore readStore, IImageUrlSigner imageSigner)
	{
		private readonly IAlpakaReadStore _readStore = readStore;
		private readonly IImageUrlSigner _imageSigner = imageSigner;

		public async Task<IReadOnlyList<AlpakaListItem>> HandleAsync(Query query, CancellationToken cancellationToken)
		{
			IReadOnlyList<AlpakaEntity> entities = await _readStore.GetAllAsync(cancellationToken).ConfigureAwait(false);
			return entities
				.Select(a => new AlpakaListItem(
					a.RowKey,
					a.Name,
					a.Geburtsdatum,
					_imageSigner.TrySignReadUrl(a.ImageUrl, TimeSpan.FromMinutes(30))))
				.ToList();
		}
	}
}
