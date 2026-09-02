using DashboardApi.Features.Gutscheine;
using DashboardApi.Tests.Fakes;
using dashboard_api.shared.entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace DashboardApi.Tests;

public sealed class RedeemGutscheinHandlerTests
{
    private static readonly DateTimeOffset Kaufdatum = new(2025, 3, 1, 0, 0, 0, TimeSpan.Zero);

    private static RedeemGutschein.Handler CreateHandler(IGutscheinStore store)
        => new(store, NullLogger<RedeemGutschein.Handler>.Instance);

    [Fact]
    public async Task Redeeming_an_open_gutschein_stores_the_date_without_its_time_component()
    {
        GutscheinEntity gutschein = InMemoryGutscheinStore.Gutschein("202501", Kaufdatum);
        InMemoryGutscheinStore store = new(gutschein);

        var (result, error) = await CreateHandler(store).HandleAsync(
            new RedeemGutschein.RedeemCommand("202501", "2025-06-15T14:30:00"),
            TestContext.Current.CancellationToken);

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Equal("202501", result.Gutscheinnummer);
        Assert.Equal("2025-06-15", result.EingeloestAm);
        Assert.Equal(new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero), gutschein.EingeloestAm);
        Assert.Equal(1, store.UpdateCount);
    }

    [Theory]
    // The day as written is the day stored, whatever the host time zone is: a bare
    // date, an instant late in the day, and an instant carrying its own offset.
    [InlineData("2025-06-15")]
    [InlineData("2025-06-15T23:30:00Z")]
    [InlineData("2025-06-15T01:30:00+02:00")]
    public async Task The_redemption_day_does_not_depend_on_the_host_time_zone(string eingeloestAm)
    {
        GutscheinEntity gutschein = InMemoryGutscheinStore.Gutschein("202501", Kaufdatum);
        InMemoryGutscheinStore store = new(gutschein);

        var (result, error) = await CreateHandler(store).HandleAsync(
            new RedeemGutschein.RedeemCommand("202501", eingeloestAm),
            TestContext.Current.CancellationToken);

        Assert.Null(error);
        Assert.Equal("2025-06-15", result!.EingeloestAm);
        Assert.Equal(new DateTimeOffset(2025, 6, 15, 0, 0, 0, TimeSpan.Zero), gutschein.EingeloestAm);
    }

    [Fact]
    public async Task Redeeming_on_the_purchase_day_itself_is_allowed()
    {
        InMemoryGutscheinStore store = new(InMemoryGutscheinStore.Gutschein("202501", Kaufdatum));

        var (result, error) = await CreateHandler(store).HandleAsync(
            new RedeemGutschein.RedeemCommand("202501", "2025-03-01"),
            TestContext.Current.CancellationToken);

        Assert.Null(error);
        Assert.Equal("2025-03-01", result!.EingeloestAm);
    }

    [Fact]
    public async Task A_row_written_by_a_non_utc_host_is_still_compared_by_its_day()
    {
        // Kaufdatum stored as local midnight in Vienna, i.e. the previous day in UTC.
        InMemoryGutscheinStore store = new(InMemoryGutscheinStore.Gutschein(
            "202501",
            new DateTimeOffset(2025, 3, 1, 0, 0, 0, TimeSpan.FromHours(2))));

        var (result, error) = await CreateHandler(store).HandleAsync(
            new RedeemGutschein.RedeemCommand("202501", "2025-03-01"),
            TestContext.Current.CancellationToken);

        Assert.Null(error);
        Assert.Equal("2025-03-01", result!.EingeloestAm);
    }

    [Fact]
    public async Task An_already_redeemed_gutschein_names_the_date_it_was_redeemed_on()
    {
        InMemoryGutscheinStore store = new(InMemoryGutscheinStore.Gutschein(
            "202501",
            Kaufdatum,
            eingeloestAm: new DateTimeOffset(2025, 4, 20, 0, 0, 0, TimeSpan.Zero)));

        var (result, error) = await CreateHandler(store).HandleAsync(
            new RedeemGutschein.RedeemCommand("202501", "2025-06-15"),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal("Der Gutschein wurde bereits am 2025-04-20 eingelöst.", error);
        Assert.Equal(0, store.UpdateCount);
    }

    [Theory]
    [InlineData("202501", "2025-02-28", "Das Einlösedatum darf nicht vor dem Kaufdatum liegen.")]
    [InlineData("202501", "irgendwann", "Das Einlösedatum ist ungültig.")]
    [InlineData("", "2025-06-15", "Die Gutscheinnummer darf nicht leer sein.")]
    [InlineData("209999", "2025-06-15", "Der Gutschein wurde nicht gefunden.")]
    public async Task Rejected_redemption_reports_the_problem_and_updates_nothing(string gutscheinnummer, string eingeloestAm, string expectedError)
    {
        InMemoryGutscheinStore store = new(InMemoryGutscheinStore.Gutschein("202501", Kaufdatum));

        var (result, error) = await CreateHandler(store).HandleAsync(
            new RedeemGutschein.RedeemCommand(gutscheinnummer, eingeloestAm),
            TestContext.Current.CancellationToken);

        Assert.Null(result);
        Assert.Equal(expectedError, error);
        Assert.Equal(0, store.UpdateCount);
    }
}
