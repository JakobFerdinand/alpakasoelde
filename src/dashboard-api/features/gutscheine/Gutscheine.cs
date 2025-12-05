using System.Net;
using System.Net.Http;
using System.Text.Json;
using Azure.Data.Tables;
using dashboard_api.shared.entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace DashboardApi.Features.Gutscheine;

public sealed class Gutscheine
{
        private readonly GetHandler _getHandler;
        private readonly AddHandler _addHandler;
        private readonly ILogger<Gutscheine> _logger;

        public Gutscheine(GetHandler getHandler, AddHandler addHandler, ILogger<Gutscheine> logger)
        {
                _getHandler = getHandler;
                _addHandler = addHandler;
                _logger = logger;
        }

        [Function("gutscheine")]
        public async Task<HttpResponseData> Run(
                [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "gutscheine")] HttpRequestData req)
        {
                if (string.Equals(req.Method, HttpMethod.Get.Method, StringComparison.OrdinalIgnoreCase))
                {
                        IReadOnlyList<GutscheinResult> vouchers = await _getHandler.HandleAsync(new GetQuery(), req.FunctionContext.CancellationToken);
                        var response = req.CreateResponse(HttpStatusCode.OK);
                        await response.WriteAsJsonAsync(vouchers).ConfigureAwait(false);
                        return response;
                }

                if (!string.Equals(req.Method, HttpMethod.Post.Method, StringComparison.OrdinalIgnoreCase))
                {
                        var methodNotAllowed = req.CreateResponse(HttpStatusCode.MethodNotAllowed);
                        await methodNotAllowed.WriteAsJsonAsync(new
                        {
                                title = "Method Not Allowed",
                                status = (int)HttpStatusCode.MethodNotAllowed
                        }).ConfigureAwait(false);
                        return methodNotAllowed;
                }

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
                        payload.Kaufdatum ?? string.Empty,
                        payload.Betrag,
                        payload.EingeloestAm);

                var (result, error) = await _addHandler.HandleAsync(command, req.FunctionContext.CancellationToken);
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

        public sealed record GetQuery;
        public sealed record AddCommand(string Kaufdatum, double? Betrag, string? EingeloestAm);
        public sealed record GutscheinResult(string VoucherNumber, string Kaufdatum, double Betrag, string? EingeloestAm);
        public sealed record AddResult(string VoucherNumber);

        public interface IGutscheinStore
        {
                Task<IReadOnlyList<GutscheinEntity>> GetAllAsync(CancellationToken cancellationToken);
                Task AddAsync(GutscheinEntity entity, CancellationToken cancellationToken);
        }

        public sealed class TableGutscheinStore(TableServiceClient tableServiceClient) : IGutscheinStore
        {
                private readonly TableServiceClient _tableServiceClient = tableServiceClient;

                public Task<IReadOnlyList<GutscheinEntity>> GetAllAsync(CancellationToken cancellationToken)
                {
                        TableClient tableClient = _tableServiceClient.GetTableClient("gutscheine");
                        var vouchers = tableClient
                                .Query<GutscheinEntity>()
                                .ToList();
                        return Task.FromResult<IReadOnlyList<GutscheinEntity>>(vouchers);
                }

                public async Task AddAsync(GutscheinEntity entity, CancellationToken cancellationToken)
                {
                        TableClient tableClient = _tableServiceClient.GetTableClient("gutscheine");
                        await tableClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
                        await tableClient.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
                }
        }

        public sealed class GetHandler(IGutscheinStore store)
        {
                private readonly IGutscheinStore _store = store;

                public async Task<IReadOnlyList<GutscheinResult>> HandleAsync(GetQuery query, CancellationToken cancellationToken)
                {
                        IReadOnlyList<GutscheinEntity> vouchers = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);

                        return vouchers
                                .OrderByDescending(voucher => voucher.PurchaseDate)
                                .ThenByDescending(voucher => voucher.VoucherNumber)
                                .Select(voucher => new GutscheinResult(
                                        voucher.VoucherNumber,
                                        voucher.PurchaseDate.ToString("yyyy-MM-dd"),
                                        voucher.Amount,
                                        voucher.RedeemedDate?.ToString("yyyy-MM-dd")))
                                .ToList();
                }
        }

        public sealed class AddHandler(IGutscheinStore store, ILogger<AddHandler> logger)
        {
                private readonly IGutscheinStore _store = store;
                private readonly ILogger<AddHandler> _logger = logger;

                private const int VoucherNumberSuffixLength = 2;

                public async Task<(AddResult? Result, string? Error)> HandleAsync(AddCommand command, CancellationToken cancellationToken)
                {
                        if (!DateTimeOffset.TryParse(command.Kaufdatum, out DateTimeOffset purchaseDate))
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

                        DateTimeOffset? redeemedDate = null;
                        if (!string.IsNullOrWhiteSpace(command.EingeloestAm))
                        {
                                redeemedDate = DateTimeOffset.Parse(command.EingeloestAm).Date;
                        }

                        if (redeemedDate is not null && redeemedDate < purchaseDate.Date)
                        {
                                return (null, "Das Einlösedatum darf nicht vor dem Kaufdatum liegen.");
                        }

                        IReadOnlyList<GutscheinEntity> existingVouchers = await _store.GetAllAsync(cancellationToken).ConfigureAwait(false);
                        string voucherNumber = GenerateVoucherNumber(existingVouchers, purchaseDate.Year);

                        GutscheinEntity entity = new()
                        {
                                VoucherNumber = voucherNumber,
                                PurchaseDate = purchaseDate.Date,
                                Amount = command.Betrag.Value,
                                RedeemedDate = redeemedDate,
                                PartitionKey = "GutscheinePartition",
                                RowKey = voucherNumber
                        };

                        await _store.AddAsync(entity, cancellationToken).ConfigureAwait(false);
                        _logger.LogInformation("Added voucher {VoucherNumber}", voucherNumber);
                        return (new AddResult(voucherNumber), null);
                }

                private static string GenerateVoucherNumber(IEnumerable<GutscheinEntity> existingVouchers, int year)
                {
                        string yearPrefix = year.ToString();
                        int highestSuffix = existingVouchers
                                .Select(v => v.VoucherNumber)
                                .Where(number => number.StartsWith(yearPrefix, StringComparison.Ordinal))
                                .Select(number => number.Length > yearPrefix.Length
                                        ? number[yearPrefix.Length..]
                                        : "0")
                                .Select(suffix => int.TryParse(suffix, out int parsed) ? parsed : 0)
                                .DefaultIfEmpty(0)
                                .Max();

                        int nextSuffix = highestSuffix + 1;
                        return $"{yearPrefix}{nextSuffix.ToString($"D{VoucherNumberSuffixLength}")}";
                }
        }

        public sealed record AddGutscheinRequest
        {
                public string? Kaufdatum { get; init; }
                public double? Betrag { get; init; }
                public string? EingeloestAm { get; init; }
        }
}
