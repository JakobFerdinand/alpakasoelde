using Azure;
using Azure.Data.Tables;

namespace Api;

public sealed class MessageEntity : ITableEntity
{
    public required string Name { get; set; }
    public required string Email { get; set; }
    public required string Message { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    public string PartitionKey { get; set; } = "ContactPartition";
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
}
