using System.Net;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace DashboardApi.Features.Gutscheine;

public sealed class GetGutscheine
{
    private readonly Handler _handler;

    public GetGutscheine(Handler handler)
    {
        _handler = handler;
    }

    [Function("get-gutscheine")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "gutscheine")] HttpRequestData req)
    {
        IReadOnlyList<GutscheinResult> vouchers = await _handler.HandleAsync(new Query(), req.FunctionContext.CancellationToken);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(vouchers).ConfigureAwait(false);
        return response;
    }

    public sealed record Query;
    public sealed record GutscheinResult(string Gutscheinnummer, string Kaufdatum, double Betrag, string? EingeloestAm);

    public sealed class Handler(IGutscheinStore store)
    {
        private readonly IGutscheinStore _store = store;

        public async Task<IReadOnlyList<GutscheinResult>> HandleAsync(Query query, CancellationToken cancellationToken)
        {
            IReadOnlyList<GutscheinEntity> vouchers = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);

            return vouchers
                .OrderByDescending(voucher => voucher.Kaufdatum)
                .ThenByDescending(voucher => voucher.Gutscheinnummer)
                .Select(voucher => new GutscheinResult(
                    voucher.Gutscheinnummer,
                    voucher.Kaufdatum.ToString("yyyy-MM-dd"),
                    voucher.Betrag,
                    voucher.EingeloestAm?.ToString("yyyy-MM-dd")))
                .ToList();
        }
    }
}
