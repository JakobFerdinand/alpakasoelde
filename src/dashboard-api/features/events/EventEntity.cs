using Azure;
using Azure.Data.Tables;

namespace DashboardApi.Features.Events;

public sealed class EventEntity : ITableEntity
{
    public required string EventType { get; set; }
    public DateTimeOffset EventDate { get; set; }
    public string? Comment { get; set; }
    public double? Cost { get; set; }
    public string SharedEventId { get; set; } = Guid.NewGuid().ToString();

    public string PartitionKey { get; set; } = string.Empty; // AlpakaId
    public string RowKey { get; set; } = Guid.NewGuid().ToString();
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
