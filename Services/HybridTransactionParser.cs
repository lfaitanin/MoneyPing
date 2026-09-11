using MoneyPing.Models;

namespace MoneyPing.Services;

public sealed class HybridTransactionParser : ITransactionParser
{
    private readonly ITransactionParser _primaryParser;
    private readonly ITransactionParser _fallbackParser;
    private readonly AdvancedIntentDetector _intentDetector;

    private static readonly string[] LlmIntentHints =
    [
        "every ",
        "daily",
        "weekly",
        "monthly",
        "yearly",
        "annually",
        "recurring",
        "subscription",
        "renews",
        "renewal",
        "until ",
        "for the next ",
        "for the following "
    ];
    public HybridTransactionParser(
        ITransactionParser primaryParser,
        ITransactionParser fallbackParser,
        AdvancedIntentDetector intentDetector)
    {
        _primaryParser = primaryParser;
        _fallbackParser = fallbackParser;
        _intentDetector = intentDetector;
    }

    public async Task<ParseResult> ParseAsync( string input, CancellationToken cancellationToken = default)
    {
        if (_intentDetector.RequiresAdvancedParsing(input))
        {
            Console.WriteLine(
                "[MoneyPing] Advanced intent detected. Using LLM parser.");

            return await _fallbackParser.ParseAsync(
                input,
                cancellationToken);
        }

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