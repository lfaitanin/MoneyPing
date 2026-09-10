using System.Globalization;
using System.Text.RegularExpressions;
using MoneyPing.Models;

namespace MoneyPing.Services;

public sealed partial class TransactionParser
{
    private static readonly string[] IncomeWords =
    [
        "received",
        "got paid",
        "earned",
        "income",
        "salary",
        "paycheck",
        "refund",
        "cashback"    
    ];
    private static readonly string[] ExpenseWords =
    [
        "spent",
        "paid",
        "bought"
    ];
    public ParseResult Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return ParseResult.Fail("Empty message.");
            
        var amountMatch = AmountRegex().Match(input);
        if (!amountMatch.Success)
            return ParseResult.Fail("Value not found Ex.: 'spent 18.50 at Lidl'.");
        
        var accountMatch = AccountRegex().Match(input);
         if (!accountMatch.Success)
            return ParseResult.Fail("Account not found: 'spent 18.50 at Lidl using AIB'.");

        var rawAmount = amountMatch.Groups["amount"].Value.Replace(',', '.');
        if (!decimal.TryParse(rawAmount, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
            return ParseResult.Fail("I couldn't interpret the value.");

        var normalized = input.Trim().ToLowerInvariant();

        var isExpense = ExpenseWords.Any(word => ContainsWord(normalized, word));
        var isIncome = IncomeWords.Any(word => ContainsWord(normalized, word));

        if (!isExpense && !isIncome)
        {
            return ParseResult.Fail(
                "Was this an expense or income? Example: 'spent 18.50 at Lidl'.");
        }

        if (isExpense && isIncome)
        {
            return ParseResult.Fail(
                "I couldn't determine whether this is an expense or income.");
        }

        amount = isIncome
            ? Math.Abs(amount)
            : -Math.Abs(amount);

        var date = normalized.Contains("ontem") || normalized.Contains("yesterday")
            ? DateTime.Today.AddDays(-1)
            : DateTime.Today;

        var title = ExtractTitle(input, amountMatch);
        if (string.IsNullOrWhiteSpace(title))
            title = isIncome ? "Income" : "Expense";

        var account = ExtractTitle(input, accountMatch);

        return ParseResult.Ok(new ParsedTransaction
        {
            Account = account,
            Amount = amount,
            Title = title,
            Date = date,
            Notes = input.Trim()
        });
    }

    private static string ExtractTitle(string input, Match amountMatch)
    {
        var text = input.Remove(amountMatch.Index, amountMatch.Length);

        text = CurrencyRegex().Replace(text, " ");
        text = VerbRegex().Replace(text, " ");
        text = DateWordRegex().Replace(text, " ");
        text = AccountRegex().Replace(text, " ");
        text = LeadingPrepositionRegex().Replace(text.Trim(), "");
        text = MultiSpaceRegex()
                .Replace(text, " ")
                .Trim(' ', '-', ':', ',', '.');
            
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        return CultureInfo.InvariantCulture.TextInfo
            .ToTitleCase(text.ToLowerInvariant());
    }

    private static bool ContainsWord(string text, string word) =>
        Regex.IsMatch(text, $@"(?<!\p{{L}}){Regex.Escape(word)}(?!\p{{L}})", RegexOptions.IgnoreCase);

    [GeneratedRegex(@"(?<!\p{L})(?:€|eur\s*)?(?<amount>\d+(?:[\.,]\d{1,2})?)(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex AmountRegex();
    
    [GeneratedRegex(@"\b(using|with)\s+(aib|revolut|cash)\b", RegexOptions.IgnoreCase)]
    private static partial Regex AccountRegex();

    [GeneratedRegex(@"\b(spent|paid|received|salary|income|refund)\b", RegexOptions.IgnoreCase)]
    private static partial Regex VerbRegex();
    
    [GeneratedRegex(@"\b(today|yesterday)\b", RegexOptions.IgnoreCase)]
    private static partial Regex DateWordRegex();

    [GeneratedRegex(@"^(at|on)\s+", RegexOptions.IgnoreCase)]
    private static partial Regex LeadingPrepositionRegex();

    [GeneratedRegex(@"\b(EUR|euro|euros)\b|€", RegexOptions.IgnoreCase)]
    private static partial Regex CurrencyRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultiSpaceRegex();
}
