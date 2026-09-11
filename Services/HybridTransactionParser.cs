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

    public async Task<ParseResult> ParseAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        var primaryResult = await _primaryParser.ParseAsync(
            input,
            cancellationToken);

       if (primaryResult.Success)
        {
            Console.WriteLine(
                "[MoneyPing] Parsed with RuleBasedTransactionParser.");

            return primaryResult;
        }

        if (primaryResult.FailureKind != ParseFailureKind.Unsupported)
        {
            Console.WriteLine(
                $"[MoneyPing] Rule-based validation failed: {primaryResult.Error}");

            return primaryResult;
        }

        Console.WriteLine(
            "[MoneyPing] Rule-based parser could not safely parse the message. Falling back to LLM.");

        return await _fallbackParser.ParseAsync(
            input,
            cancellationToken);
    }
}