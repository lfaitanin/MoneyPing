using MoneyPing.Models;

namespace MoneyPing.Services;

public sealed class HybridTransactionParser : ITransactionParser
{
    private readonly ITransactionParser _primaryParser;
    private readonly ITransactionParser _fallbackParser;

    public HybridTransactionParser(
        ITransactionParser primaryParser,
        ITransactionParser fallbackParser)
    {
        _primaryParser = primaryParser;
        _fallbackParser = fallbackParser;
    }

    public ParseResult Parse(string input)
    {
        var primaryResult = _primaryParser.Parse(input);

        if (primaryResult.Success)
            return primaryResult;

        return _fallbackParser.Parse(input);
    }
}