using Azure;
using Azure.Data.Tables;

namespace dashboard_api.shared.entities;

public sealed class GutscheinEntity : ITableEntity
{
        public required string Gutscheinnummer { get; set; }
        public DateTimeOffset Kaufdatum { get; set; }
        public double Betrag { get; set; }
        public DateTimeOffset? EingeloestAm { get; set; }

        public string PartitionKey { get; set; } = "GutscheinePartition";
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
}
