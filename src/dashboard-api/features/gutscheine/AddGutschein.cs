using System.Net;
using System.Text.Json;
using System.Linq;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Gutscheine;

public sealed class AddGutschein
{
    private readonly Handler _handler;
    private readonly ILogger<AddGutschein> _logger;

    public AddGutschein(Handler handler, ILogger<AddGutschein> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    [Function("add-gutschein")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "gutscheine")] HttpRequestData req)
    {
        AddGutscheinRequest? payload;
        try
        {
            payload = await JsonSerializer.DeserializeAsync<AddGutscheinRequest>(req.Body, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON payload for add-gutschein.");
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                title = "Bad Request",
                status = (int)HttpStatusCode.BadRequest,
                detail = "Ungültiger Anfrageinhalt."
            }).ConfigureAwait(false);
            return badRequest;
        }

        if (payload is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                title = "Bad Request",
                status = (int)HttpStatusCode.BadRequest,
                detail = "Ein Gutschein muss angegeben werden."
            }).ConfigureAwait(false);
            return badRequest;
        }

        AddCommand command = new(
            payload.Gutscheinnummer,
            payload.Kaufdatum ?? string.Empty,
            payload.Betrag,
            payload.EingeloestAm,
            payload.VerkauftAn);

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

        var created = req.CreateResponse(HttpStatusCode.Created);
        await created.WriteAsJsonAsync(result).ConfigureAwait(false);
        return created;
    }

    public sealed record AddCommand(string? Gutscheinnummer, string Kaufdatum, double? Betrag, string? EingeloestAm, string? VerkauftAn);
    public sealed record AddResult(string Gutscheinnummer);
    public sealed record AddGutscheinRequest
    {
        public string? Gutscheinnummer { get; init; }
        public string? Kaufdatum { get; init; }
        public double? Betrag { get; init; }
        public string? EingeloestAm { get; init; }
        public string? VerkauftAn { get; init; }
    }

    public sealed class Handler(IGutscheinStore store, ILogger<Handler> logger)
    {
        private readonly IGutscheinStore _store = store;
        private readonly ILogger<Handler> _logger = logger;

        private const int GutscheinSuffixLength = 2;

        public async Task<(AddResult? Result, string? Error)> HandleAsync(AddCommand command, CancellationToken cancellationToken)
        {
            if (!DateTimeOffset.TryParse(command.Kaufdatum, out DateTimeOffset kaufdatum))
            {
                return (null, "Das Kaufdatum ist ungültig.");
            }

            if (command.Betrag is null or <= 0)
            {
                return (null, "Der Betrag muss größer als 0 sein.");
            }

            if (!string.IsNullOrWhiteSpace(command.EingeloestAm) && !DateTimeOffset.TryParse(command.EingeloestAm, out _))
            {
                return (null, "Das Einlösedatum ist ungültig.");
            }

            string? verkauftAn = string.IsNullOrWhiteSpace(command.VerkauftAn)
                ? null
                : command.VerkauftAn.Trim();
            if (verkauftAn is not null && verkauftAn.Length > 200)
            {
                return (null, "Der Name des Käufers darf höchstens 200 Zeichen enthalten.");
            }

            DateTimeOffset? eingeloestAm = null;
            if (!string.IsNullOrWhiteSpace(command.EingeloestAm))
            {
                eingeloestAm = DateTimeOffset.Parse(command.EingeloestAm).Date;
            }

            if (eingeloestAm is not null && eingeloestAm < kaufdatum.Date)
            {
                return (null, "Das Einlösedatum darf nicht vor dem Kaufdatum liegen.");
            }

            IReadOnlyList<GutscheinEntity> existingGutscheine = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);
            string gutscheinnummer = command.Gutscheinnummer?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(gutscheinnummer))
            {
                if (!IsValidGutscheinNumberForYear(gutscheinnummer, kaufdatum.Year))
                {
                    return (null, $"Die Gutscheinnummer muss mit {kaufdatum.Year} beginnen und mindestens zwei Ziffern enthalten.");
                }

                if (existingGutscheine.Any(existing => string.Equals(existing.Gutscheinnummer, gutscheinnummer, StringComparison.Ordinal)))
                {
                    return (null, "Die angegebene Gutscheinnummer existiert bereits.");
                }
            }
            else
            {
                gutscheinnummer = GenerateGutscheinNumber(existingGutscheine, kaufdatum.Year);
            }

            GutscheinEntity entity = new()
            {
                Gutscheinnummer = gutscheinnummer,
                Kaufdatum = kaufdatum.Date,
                Betrag = command.Betrag.Value,
                EingeloestAm = eingeloestAm,
                VerkauftAn = verkauftAn,
                PartitionKey = "GutscheinePartition",
                RowKey = gutscheinnummer
            };

            await _store.AddAsync(entity, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Gutschein {Gutscheinnummer} gespeichert", gutscheinnummer);
            return (new AddResult(gutscheinnummer), null);
        }

        private static string GenerateGutscheinNumber(IEnumerable<GutscheinEntity> existingGutscheine, int year)
        {
            string yearPrefix = year.ToString();
            int highestSuffix = existingGutscheine
                .Select(v => v.Gutscheinnummer)
                .Where(number => number.StartsWith(yearPrefix, StringComparison.Ordinal))
                .Select(number => number.Length > yearPrefix.Length
                    ? number[yearPrefix.Length..]
                    : "0")
                .Select(suffix => int.TryParse(suffix, out int parsed) ? parsed : 0)
                .DefaultIfEmpty(0)
                .Max();

            int nextSuffix = highestSuffix + 1;
            return $"{yearPrefix}{nextSuffix.ToString($"D{GutscheinSuffixLength}")}";
        }

        private static bool IsValidGutscheinNumberForYear(string gutscheinnummer, int year)
        {
            if (string.IsNullOrWhiteSpace(gutscheinnummer))
            {
                return false;
            }

            string expectedPrefix = year.ToString();
            if (!gutscheinnummer.StartsWith(expectedPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string suffix = gutscheinnummer[expectedPrefix.Length..];
            return suffix.Length >= GutscheinSuffixLength && suffix.All(char.IsDigit);
        }
    }
}
