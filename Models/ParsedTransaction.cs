namespace MoneyPing.Models;

public sealed class ParsedTransaction
{
    
    public required decimal Amount { get; init; }
    public required string Title { get; init; }
    public required DateTime Date { get; init; }
    public string? Category { get; set; }
    public string? Account { get; set; }
    public string? Notes { get; init; }
    public bool IsIncome => Amount > 0;
}

public sealed record ParseResult(bool Success, ParsedTransaction? Transaction, string? Error)
{
    public static ParseResult Ok(ParsedTransaction transaction) => new(true, transaction, null);
    public static ParseResult Fail(string error) => new(false, null, error);
}

