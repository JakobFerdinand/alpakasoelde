using Azure;
using Azure.Data.Tables;

namespace dashboard_api.shared.entities;

public sealed class AlpakaEntity : ITableEntity
{
	public required string Name { get; set; }
	public required string Geburtsdatum { get; set; }
	public string? ImageUrl { get; set; }

	public string PartitionKey { get; set; } = "AlpakaPartition";
	public string RowKey { get; set; } = Guid.NewGuid().ToString();
	public DateTimeOffset? Timestamp { get; set; }
	public ETag ETag { get; set; }
}
