using MoneyPing.Models;

namespace MoneyPing.Services;

public sealed class FallbackTransactionParser : ITransactionParser
{
    public ParseResult Parse(string input)
    {
        return ParseResult.Fail(
            "This transaction is too complex for the current parser.");
    }
}