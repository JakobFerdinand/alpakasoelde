using System.Net;
using System.Text.Json;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Gutscheine;

public sealed class RedeemGutschein
{
    private readonly Handler _handler;
    private readonly ILogger<RedeemGutschein> _logger;

    public RedeemGutschein(Handler handler, ILogger<RedeemGutschein> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [Function("redeem-gutschein")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "gutscheine/{gutscheinnummer}/einloesen")] HttpRequestData req,
        string gutscheinnummer)
    {
        RedeemGutscheinRequest? payload;
        try
        {
            payload = await req.ReadFromJsonAsync<RedeemGutscheinRequest>(new() { PropertyNameCaseInsensitive = true }).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is JsonException or FormatException)
        {
            _logger.LogWarning(ex, "Invalid JSON payload for redeem-gutschein.");
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                title = "Bad Request",
                status = (int)HttpStatusCode.BadRequest,
                detail = "Ungültiger Anfrageinhalt."
            }).ConfigureAwait(false);
            return badRequest;
        }

        RedeemCommand command = new(gutscheinnummer, payload?.EingeloestAm ?? string.Empty);
        var (result, error) = await _handler.HandleAsync(command, req.FunctionContext.CancellationToken);

        if (error is not null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                title = "Bad Request",
                status = (int)HttpStatusCode.BadRequest,
                detail = error
            }).ConfigureAwait(false);
            return badRequest;
        }

        var ok = req.CreateResponse(HttpStatusCode.OK);
        await ok.WriteAsJsonAsync(result).ConfigureAwait(false);
        return ok;
    }

    public sealed record RedeemCommand(string Gutscheinnummer, string EingeloestAm);
    public sealed record RedeemResult(string Gutscheinnummer, string EingeloestAm);

    public sealed record RedeemGutscheinRequest
    {
        public string? EingeloestAm { get; init; }
    }

    public sealed class Handler(IGutscheinStore store, ILogger<Handler> logger)
    {
        private readonly IGutscheinStore _store = store;
        private readonly ILogger<Handler> _logger = logger;

        public async Task<(RedeemResult? Result, string? Error)> HandleAsync(RedeemCommand command, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(command.Gutscheinnummer))
            {
                return (null, "Die Gutscheinnummer darf nicht leer sein.");
            }

            if (!DateTimeOffset.TryParse(command.EingeloestAm, out DateTimeOffset eingeloestAm))
            {
                return (null, "Das Einlösedatum ist ungültig.");
            }

            GutscheinEntity? existing = await _store.GetByGutscheinnummerAsync(command.Gutscheinnummer, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return (null, "Der Gutschein wurde nicht gefunden.");
            }

            if (existing.EingeloestAm is not null)
            {
                return (null, $"Der Gutschein wurde bereits am {existing.EingeloestAm.Value:yyyy-MM-dd} eingelöst.");
            }

            DateTimeOffset normalizedEingeloestAm = eingeloestAm.Date;
            if (normalizedEingeloestAm < existing.Kaufdatum.Date)
            {
                return (null, "Das Einlösedatum darf nicht vor dem Kaufdatum liegen.");
            }

            existing.EingeloestAm = normalizedEingeloestAm;
            await _store.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Gutschein {Gutscheinnummer} wurde eingelöst", command.Gutscheinnummer);

            return (new RedeemResult(existing.Gutscheinnummer, normalizedEingeloestAm.ToString("yyyy-MM-dd")), null);
        }
    }
}
