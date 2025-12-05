using Azure;
using Azure.Data.Tables;

namespace dashboard_api.shared.entities;

public sealed class GutscheinEntity : ITableEntity
{
        public required string VoucherNumber { get; set; }
        public DateTimeOffset PurchaseDate { get; set; }
        public double Amount { get; set; }
        public DateTimeOffset? RedeemedDate { get; set; }

        public string PartitionKey { get; set; } = "GutscheinePartition";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
}
