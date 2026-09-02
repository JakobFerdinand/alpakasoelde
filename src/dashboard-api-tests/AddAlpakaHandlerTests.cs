using DashboardApi.Features.Alpakas;
using dashboard_api.shared.entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashboardApi.Tests;

public sealed class AddAlpakaHandlerTests
{
	private sealed class RecordingWriteStore : AddAlpaka.IAlpakaWriteStore
	{
		public List<AlpakaEntity> Written { get; } = [];

		public Task AddAsync(AlpakaEntity entity, CancellationToken cancellationToken)
		{
			Written.Add(entity);
			return Task.CompletedTask;
		}
	}

	private sealed class StubImageStore(string? uploadedUrl) : AddAlpaka.IAlpakaImageStore
	{
		public int UploadCount { get; private set; }

		public Task<string?> UploadAsync(AddAlpaka.AlpakaImagePayload image, CancellationToken cancellationToken)
		{
			UploadCount++;
			return Task.FromResult(uploadedUrl);
		}
	}

	/// A stream that only claims to be large, so the size guard can be exercised
	/// without allocating 15MB.
	private sealed class OversizedStream(long length) : MemoryStream
	{
		public override long Length => length;
	}

	private const long MaxImageSizeBytes = 15 * 1024 * 1024;

	private static AddAlpaka.AlpakaImagePayload Image(string fileName = "richard.jpg", Stream? content = null)
		=> new(content ?? new MemoryStream([1, 2, 3]), fileName, "image/jpeg");

	private static (AddAlpaka.Handler Handler, RecordingWriteStore Store, StubImageStore Images) CreateHandler(string? uploadedUrl = "https://storage/alpakas/richard.jpg")
	{
		RecordingWriteStore store = new();
		StubImageStore images = new(uploadedUrl);
		return (new AddAlpaka.Handler(store, images, NullLogger<AddAlpaka.Handler>.Instance), store, images);
	}

	[Fact]
	public async Task An_alpaka_without_an_image_is_stored_on_the_alpaka_partition()
	{
		var (handler, store, images) = CreateHandler();

		AddAlpaka.Response response = await handler.HandleAsync(
			new AddAlpaka.Command("  Richard  ", "  2019-04-01  ", null),
			TestContext.Current.CancellationToken);

		Assert.True(response.IsValid);
		AlpakaEntity stored = Assert.Single(store.Written);
		Assert.Equal("AlpakaPartition", stored.PartitionKey);
		Assert.Equal("Richard", stored.Name);
		Assert.Equal("2019-04-01", stored.Geburtsdatum);
		Assert.Null(stored.ImageUrl);
		Assert.Equal(0, images.UploadCount);
		Assert.Equal(new AddAlpaka.Result(stored.RowKey, "Richard", "2019-04-01", null), response.Result);
	}

	[Fact]
	public async Task An_uploaded_image_url_is_stored_on_the_alpaka()
	{
		var (handler, store, images) = CreateHandler();

		AddAlpaka.Response response = await handler.HandleAsync(
			new AddAlpaka.Command("Richard", "2019-04-01", Image()),
			TestContext.Current.CancellationToken);

		Assert.True(response.IsValid);
		Assert.Equal(1, images.UploadCount);
		Assert.Equal("https://storage/alpakas/richard.jpg", Assert.Single(store.Written).ImageUrl);
	}

	[Fact]
	public async Task An_image_the_store_refuses_leaves_the_alpaka_without_a_picture()
	{
		// The blob store returns null for anything that is not png/jpg/jpeg, and the
		// alpaka is still created rather than the whole request failing.
		var (handler, store, images) = CreateHandler(uploadedUrl: null);

		AddAlpaka.Response response = await handler.HandleAsync(
			new AddAlpaka.Command("Richard", "2019-04-01", Image("richard.gif")),
			TestContext.Current.CancellationToken);

		Assert.True(response.IsValid);
		Assert.Equal(1, images.UploadCount);
		Assert.Null(Assert.Single(store.Written).ImageUrl);
	}

	[Fact]
	public async Task An_oversized_image_is_rejected_before_anything_is_uploaded_or_stored()
	{
		var (handler, store, images) = CreateHandler();

		AddAlpaka.Response response = await handler.HandleAsync(
			new AddAlpaka.Command("Richard", "2019-04-01", Image(content: new OversizedStream(MaxImageSizeBytes + 1))),
			TestContext.Current.CancellationToken);

		Assert.False(response.IsValid);
		Assert.Equal(["Image file exceeds the maximum allowed size of 15MB."], response.ValidationErrors);
		Assert.Equal(0, images.UploadCount);
		Assert.Empty(store.Written);
	}

	[Fact]
	public async Task An_image_exactly_on_the_limit_is_accepted()
	{
		var (handler, store, _) = CreateHandler();

		AddAlpaka.Response response = await handler.HandleAsync(
			new AddAlpaka.Command("Richard", "2019-04-01", Image(content: new OversizedStream(MaxImageSizeBytes))),
			TestContext.Current.CancellationToken);

		Assert.True(response.IsValid);
		Assert.Single(store.Written);
	}

	[Fact]
	public async Task Missing_fields_are_named_together_and_nothing_is_stored()
	{
		var (handler, store, _) = CreateHandler();

		AddAlpaka.Response response = await handler.HandleAsync(
			new AddAlpaka.Command("   ", "", null),
			TestContext.Current.CancellationToken);

		Assert.False(response.IsValid);
		Assert.Equal(["Name", "Geburtsdatum"], response.ValidationErrors);
		Assert.Empty(store.Written);
	}

	[Fact]
	public async Task An_oversized_name_is_rejected()
	{
		var (handler, store, _) = CreateHandler();

		AddAlpaka.Response response = await handler.HandleAsync(
			new AddAlpaka.Command(new string('n', 101), "2019-04-01", null),
			TestContext.Current.CancellationToken);

		Assert.False(response.IsValid);
		Assert.Equal(["Name exceeds 100 characters."], response.ValidationErrors);
		Assert.Empty(store.Written);
	}
}
