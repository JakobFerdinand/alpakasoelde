namespace Api;

public sealed class EmailOptions
{
    public required string? SenderAddress { get; init; }
    public required string[] ReceiverAddresses { get; init; }
    public required string? ConnectionString { get; init; }
}
