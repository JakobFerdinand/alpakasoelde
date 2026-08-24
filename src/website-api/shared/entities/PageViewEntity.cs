using Azure;
using Azure.Data.Tables;

namespace website_api.shared.entities;

public sealed class PageViewEntity : ITableEntity
{
	public required string Path { get; set; }
	public string? ReferrerHost { get; set; }
	public int ViewportWidth { get; set; }
	public string? SessionId { get; set; }
	public string? VisitorId { get; set; }
	public string? NavigationType { get; set; }

	public DateTimeOffset? Timestamp { get; set; }
	public ETag ETag { get; set; }
	public string PartitionKey { get; set; } = string.Empty;
	public string RowKey { get; set; } = Guid.NewGuid().ToString();
}