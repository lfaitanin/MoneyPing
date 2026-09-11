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

public sealed record ParseResult(
    bool Success,
    IReadOnlyList<ParsedTransaction> Transactions,
    string? Error,
    ParseFailureKind FailureKind)
{
    public ParsedTransaction? Transaction =>
        Transactions.FirstOrDefault();

    public static ParseResult Ok(ParsedTransaction transaction) =>
        new(
            true,
            [transaction],
            null,
            ParseFailureKind.None);

    public static ParseResult Ok(
        IEnumerable<ParsedTransaction> transactions) =>
        new(
            true,
            transactions.ToList(),
            null,
            ParseFailureKind.None);

    public static ParseResult Fail(string error) =>
        new(
            false,
            [],
            error,
            ParseFailureKind.Validation);

    public static ParseResult Unsupported(string error) =>
        new(
            false,
            [],
            error,
            ParseFailureKind.Unsupported);
}

