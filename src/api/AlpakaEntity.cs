using Azure;
using Azure.Data.Tables;

namespace WebsiteApi;

public sealed class AlpakaEntity : ITableEntity
{
    public required string Name { get; set; }
    public required string Geburtsdatum { get; set; }
    public string? ImageUrl { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string PartitionKey { get; set; } = "AlpakaPartition";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
}
