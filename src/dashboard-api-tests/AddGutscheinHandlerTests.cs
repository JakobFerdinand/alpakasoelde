using DashboardApi.Features.Gutscheine;
using DashboardApi.Tests.Fakes;
using dashboard_api.shared.entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashboardApi.Tests;

public sealed class AddGutscheinHandlerTests
{
    private static GutscheinEntity Purchased(string gutscheinnummer)
        => InMemoryGutscheinStore.Gutschein(gutscheinnummer, new DateTimeOffset(2025, 1, 2, 0, 0, 0, TimeSpan.Zero));

    private static AddGutschein.Handler CreateHandler(IGutscheinStore store)
        => new(store, NullLogger<AddGutschein.Handler>.Instance);

    [Fact]
    public async Task Blank_number_continues_the_highest_suffix_of_the_purchase_year()
    {
        // 2024 numbers must not raise the 2025 counter, and the suffix stays two digits.
        InMemoryGutscheinStore store = new(Purchased("202417"), Purchased("202503"), Purchased("202511"));
        AddGutschein.Handler handler = CreateHandler(store);

        var (result, error) = await handler.HandleAsync(
            new AddGutschein.AddCommand(null, "2025-06-15", 75, null, "  Anna Huber  "),
            TestContext.Current.CancellationToken);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("202512", result.Gutscheinnummer);

        GutscheinEntity stored = Assert.Single(store.Entities, e => e.Gutscheinnummer == "202512");
        Assert.Equal("GutscheinePartition", stored.PartitionKey);
        Assert.Equal("202512", stored.RowKey);
        Assert.Equal("Anna Huber", stored.VerkauftAn);
        Assert.Equal(75, stored.Betrag);
        Assert.Equal(new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero), stored.Kaufdatum);
        Assert.Null(stored.EingeloestAm);
    }

    [Theory]
    // The day as written is the day stored, whatever the host time zone is: a bare
    // date, an instant late in the day, and an instant carrying its own offset —
    // which also decides the year the number is issued for.
    [InlineData("2025-06-15")]
    [InlineData("2025-06-15T23:30:00Z")]
    [InlineData("2025-06-15T01:30:00+02:00")]
    public async Task The_purchase_day_does_not_depend_on_the_host_time_zone(string kaufdatum)
    {
        InMemoryGutscheinStore store = new();
        AddGutschein.Handler handler = CreateHandler(store);

        var (result, error) = await handler.HandleAsync(
            new AddGutschein.AddCommand(null, kaufdatum, 75, null, null),
            TestContext.Current.CancellationToken);

        Assert.Null(error);
        Assert.Equal("202501", result!.Gutscheinnummer);
        Assert.Equal(new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero), Assert.Single(store.Entities).Kaufdatum);
    }

    [Fact]
    public async Task A_redemption_date_is_stored_as_a_plain_day_too()
    {
        InMemoryGutscheinStore store = new();
        AddGutschein.Handler handler = CreateHandler(store);

        var (_, error) = await handler.HandleAsync(
            new AddGutschein.AddCommand(null, "2025-06-15", 75, "2025-07-01T18:45:00Z", null),
            TestContext.Current.CancellationToken);

        Assert.Null(error);
        Assert.Equal(new DateTimeOffset(2025, 7, 1, 0, 0, 0, TimeSpan.Zero), Assert.Single(store.Entities).EingeloestAm);
    }

    [Theory]
    [InlineData("202417", null, "Die Gutscheinnummer muss mit 2025 beginnen und mindestens zwei Ziffern enthalten.")]
    [InlineData(null, "2025-06-01", "Das Einlösedatum darf nicht vor dem Kaufdatum liegen.")]
    public async Task Rejected_gutschein_reports_the_problem_and_writes_nothing(string? gutscheinnummer, string? eingeloestAm, string expectedError)
    {
        InMemoryGutscheinStore store = new();
        AddGutschein.Handler handler = CreateHandler(store);

        var (result, error) = await handler.HandleAsync(
            new AddGutschein.AddCommand(gutscheinnummer, "2025-06-15", 75, eingeloestAm, null),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(expectedError, error);
        Assert.Empty(store.Entities);
    }
}
