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
        Assert.Null(stored.EingeloestAm);
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
