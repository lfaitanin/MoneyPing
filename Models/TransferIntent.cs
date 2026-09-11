namespace MoneyPing.Models;

public sealed class TransferIntent
{
    public decimal Amount { get; init; }
    public required string SourceAccount { get; init; }
    public required string DestinationAccount { get; init; }
    public DateTime Date { get; init; }
    public string? Notes { get; init; }
}