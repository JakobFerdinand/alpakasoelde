using DashboardApi.Features.Alpakas;
using DashboardApi.Tests.Fakes;
using dashboard_api.shared.entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashboardApi.Tests;

public sealed class UpdateAlpakaHandlerTests
{
	private sealed class RecordingUpdateStore(AlpakaEntity? existing) : UpdateAlpaka.IAlpakaUpdateStore
	{
		public List<(AlpakaEntity Entity, Azure.ETag ETag)> Updates { get; } = [];

		public Task<AlpakaEntity?> GetAsync(string id, CancellationToken cancellationToken)
			=> Task.FromResult(existing is not null && existing.RowKey == id ? existing : null);

		public Task UpdateAsync(AlpakaEntity entity, Azure.ETag etag, CancellationToken cancellationToken)
		{
			Updates.Add((entity, etag));
			return Task.CompletedTask;
		}
	}

	private sealed class StubImageReplacementStore(string? replacedUrl = null, string? failWith = null) : UpdateAlpaka.IAlpakaImageReplacementStore
	{
		public List<string?> ReplacedFrom { get; } = [];

		public Task<string?> ReplaceAsync(string? existingUrl, AddAlpaka.AlpakaImagePayload newImage, CancellationToken cancellationToken)
		{
			ReplacedFrom.Add(existingUrl);
			return failWith is null
				? Task.FromResult(replacedUrl)
				: throw new InvalidOperationException(failWith);
		}
	}

	private static AlpakaEntity Existing() => new()
	{
		Name = "Richard",
		Geburtsdatum = "2019-04-01",
		ImageUrl = "https://storage/alpakas/alt.jpg",
		RowKey = "alpaka-1",
		ETag = new Azure.ETag("W/\"etag-1\"")
	};

	private static AddAlpaka.AlpakaImagePayload Image()
		=> new(new MemoryStream([1, 2, 3]), "neu.jpg", "image/jpeg");

	[Fact]
	public async Task An_update_persists_the_trimmed_fields_under_the_entity_etag_and_returns_a_signed_url()
	{
		AlpakaEntity existing = Existing();
		RecordingUpdateStore store = new(existing);
		RecordingImageUrlSigner signer = new();
		UpdateAlpaka.Handler handler = new(store, new StubImageReplacementStore(), NullLogger<UpdateAlpaka.Handler>.Instance, signer);

		UpdateAlpaka.Response response = await handler.HandleAsync(
			new UpdateAlpaka.Command("alpaka-1", "  Richard II  ", "  2019-04-02  ", null),
			TestContext.Current.CancellationToken);

		Assert.True(response.IsValid);
		Assert.False(response.NotFound);

		var (entity, etag) = Assert.Single(store.Updates);
		Assert.Equal("Richard II", entity.Name);
		Assert.Equal("2019-04-02", entity.Geburtsdatum);
		// Optimistic concurrency: the update goes out under the etag it was read with.
		Assert.Equal(new Azure.ETag("W/\"etag-1\""), etag);

		Assert.Equal("https://storage/alpakas/alt.jpg?sas", response.Result!.ImageUrl);
		Assert.Equal([("https://storage/alpakas/alt.jpg", TimeSpan.FromMinutes(30))], signer.Calls);
	}

	[Fact]
	public async Task A_new_image_replaces_the_previous_one()
	{
		AlpakaEntity existing = Existing();
		RecordingUpdateStore store = new(existing);
		StubImageReplacementStore images = new(replacedUrl: "https://storage/alpakas/neu.jpg");
		UpdateAlpaka.Handler handler = new(store, images, NullLogger<UpdateAlpaka.Handler>.Instance, new RecordingImageUrlSigner());

		UpdateAlpaka.Response response = await handler.HandleAsync(
			new UpdateAlpaka.Command("alpaka-1", "Richard", "2019-04-01", Image()),
			TestContext.Current.CancellationToken);

		Assert.True(response.IsValid);
		// The old blob url is handed over so the replacement store can delete it.
		Assert.Equal(["https://storage/alpakas/alt.jpg"], images.ReplacedFrom);
		Assert.Equal("https://storage/alpakas/neu.jpg", Assert.Single(store.Updates).Entity.ImageUrl);
		Assert.Equal("https://storage/alpakas/neu.jpg?sas", response.Result!.ImageUrl);
	}

	[Fact]
	public async Task A_rejected_image_becomes_a_validation_error_and_the_alpaka_is_left_alone()
	{
		RecordingUpdateStore store = new(Existing());
		UpdateAlpaka.Handler handler = new(
			store,
			new StubImageReplacementStore(failWith: "Unsupported image file type. Only .png, .jpg or .jpeg is allowed."),
			NullLogger<UpdateAlpaka.Handler>.Instance,
			new RecordingImageUrlSigner());

		UpdateAlpaka.Response response = await handler.HandleAsync(
			new UpdateAlpaka.Command("alpaka-1", "Richard", "2019-04-01", Image()),
			TestContext.Current.CancellationToken);

		Assert.False(response.IsValid);
		Assert.Equal(["Unsupported image file type. Only .png, .jpg or .jpeg is allowed."], response.ValidationErrors);
		Assert.Empty(store.Updates);
	}

	[Fact]
	public async Task An_unknown_alpaka_reports_not_found_rather_than_a_validation_error()
	{
		RecordingUpdateStore store = new(Existing());
		UpdateAlpaka.Handler handler = new(store, new StubImageReplacementStore(), NullLogger<UpdateAlpaka.Handler>.Instance, new RecordingImageUrlSigner());

		UpdateAlpaka.Response response = await handler.HandleAsync(
			new UpdateAlpaka.Command("gibt-es-nicht", "Richard", "2019-04-01", null),
			TestContext.Current.CancellationToken);

		Assert.True(response.NotFound);
		Assert.True(response.IsValid);
		Assert.Null(response.Result);
		Assert.Empty(store.Updates);
	}

	[Theory]
	[InlineData("   ", "2019-04-01", "Name")]
	[InlineData("Richard", "  ", "Geburtsdatum")]
	public async Task Missing_fields_are_rejected_before_the_alpaka_is_even_looked_up(string name, string geburtsdatum, string expectedError)
	{
		RecordingUpdateStore store = new(Existing());
		UpdateAlpaka.Handler handler = new(store, new StubImageReplacementStore(), NullLogger<UpdateAlpaka.Handler>.Instance, new RecordingImageUrlSigner());

		UpdateAlpaka.Response response = await handler.HandleAsync(
			new UpdateAlpaka.Command("alpaka-1", name, geburtsdatum, null),
			TestContext.Current.CancellationToken);

		Assert.False(response.IsValid);
		Assert.Equal([expectedError], response.ValidationErrors);
		Assert.False(response.NotFound);
		Assert.Empty(store.Updates);
	}

	[Fact]
	public async Task An_oversized_name_is_rejected()
	{
		RecordingUpdateStore store = new(Existing());
		UpdateAlpaka.Handler handler = new(store, new StubImageReplacementStore(), NullLogger<UpdateAlpaka.Handler>.Instance, new RecordingImageUrlSigner());

		UpdateAlpaka.Response response = await handler.HandleAsync(
			new UpdateAlpaka.Command("alpaka-1", new string('n', 101), "2019-04-01", null),
			TestContext.Current.CancellationToken);

		Assert.False(response.IsValid);
		Assert.Equal(["Name exceeds 100 characters."], response.ValidationErrors);
		Assert.Empty(store.Updates);
	}
}
