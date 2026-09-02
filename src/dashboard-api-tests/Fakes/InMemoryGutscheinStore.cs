using DashboardApi.Features.Gutscheine;
using dashboard_api.shared.entities;

namespace DashboardApi.Tests.Fakes;

internal sealed class InMemoryGutscheinStore(params GutscheinEntity[] seed) : IGutscheinStore
{
    public List<GutscheinEntity> Entities { get; } = [.. seed];

    public int UpdateCount { get; private set; }

    public Task<IReadOnlyList<GutscheinEntity>> GetAllAsync(CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<GutscheinEntity>>(Entities);

    public Task AddAsync(GutscheinEntity entity, CancellationToken cancellationToken)
    {
        Entities.Add(entity);
        return Task.CompletedTask;
    }

    public Task<GutscheinEntity?> GetByGutscheinnummerAsync(string gutscheinnummer, CancellationToken cancellationToken)
        => Task.FromResult(Entities.Find(e => e.Gutscheinnummer == gutscheinnummer));

    public Task UpdateAsync(GutscheinEntity entity, CancellationToken cancellationToken)
    {
        UpdateCount++;
        return Task.CompletedTask;
    }

    public static GutscheinEntity Gutschein(string gutscheinnummer, DateTimeOffset kaufdatum, DateTimeOffset? eingeloestAm = null) => new()
    {
        Gutscheinnummer = gutscheinnummer,
        RowKey = gutscheinnummer,
        Kaufdatum = kaufdatum,
        Betrag = 50,
        EingeloestAm = eingeloestAm
    };
}
