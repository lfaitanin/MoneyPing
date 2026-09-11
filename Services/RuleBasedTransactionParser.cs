using System.Globalization;
using System.Text.RegularExpressions;
using MoneyPing.Models;

namespace MoneyPing.Services;

public sealed partial class RuleBasedTransactionParser : ITransactionParser
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
    public Task<ParseResult> ParseAsync(string input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult(ParseResult.Fail("Empty message."));

        var normalized = input.Trim().ToLowerInvariant();

        var isExpense = ExpenseWords.Any(word =>
            ContainsWord(normalized, word));

        var isIncome = IncomeWords.Any(word =>
            ContainsWord(normalized, word));

        if (!isExpense && !isIncome)
            return Task.FromResult(ParseResult.Fail(
                "Was this an expense or income? Example: 'spent 18.50 at Lidl'."));

        if (isExpense && isIncome)
            return Task.FromResult(ParseResult.Fail(
                "I couldn't determine whether this is an expense or income."));

        var accountMatch = AccountRegex().Match(input);

        if (!accountMatch.Success)
            return Task.FromResult(ParseResult.Fail(
                "Account not found. Example: 'spent 18.50 at Lidl using AIB'."));

        var account = NormalizeAccount(accountMatch.Groups["account"].Value);
        
        var date = normalized.Contains("yesterday")
            ? DateTime.Today.AddDays(-1)
            : DateTime.Today;

        var parts = TransactionSeparatorRegex().Split(input);

        var amountMatches = AmountRegex().Matches(input);

        if (amountMatches.Count != parts.Length) 
            return Task.FromResult(ParseResult.Unsupported("Message structure is too complex for the rule-based parser."));
        
        var transactions = new List<ParsedTransaction>();

        foreach (var part in parts)
        {
            var result = ParseSingleTransaction(
                part,
                isIncome,
                account,
                date);

            if (!result.Success || result.Transaction is null)
            {
                return Task.FromResult(ParseResult.Fail(
                    result.Error ?? $"Could not parse transaction: {part}"));
            }

            transactions.Add(result.Transaction);
        }

        return Task.FromResult(ParseResult.Ok(transactions));
    }
    private ParseResult ParseSingleTransaction(string input, bool isIncome, string account, DateTime date)
    {
        var amountMatch = AmountRegex().Match(input);

        if (!amountMatch.Success)
            return ParseResult.Fail(
                $"Amount not found in transaction: '{input.Trim()}'.");
        
        var rawAmount = amountMatch
            .Groups["amount"]
            .Value
            .Replace(',', '.');

        if (!decimal.TryParse(
                rawAmount,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount) ||
            amount <= 0)
        {
            return ParseResult.Fail(
                $"Could not parse amount in transaction: '{input.Trim()}'.");
        }

        amount = isIncome
            ? Math.Abs(amount)
            : -Math.Abs(amount);

        var title = ExtractTitle(input, amountMatch);

        if (string.IsNullOrWhiteSpace(title))
        {
            title = isIncome
                ? "Income"
                : "Expense";
        }

        return ParseResult.Ok(
            new ParsedTransaction
            {
                Amount = amount,
                Title = title,
                Date = date,
                Account = account,
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

    private static string NormalizeAccount(string account)
    {
        return account.ToLowerInvariant() switch
        {
            "aib" => "AIB",
            "revolut" => "Revolut",
            "cash" => "Cash",
            _ => account
        };
    }
    private static bool ContainsWord(string text, string word) =>
        Regex.IsMatch(text, $@"(?<!\p{{L}}){Regex.Escape(word)}(?!\p{{L}})", RegexOptions.IgnoreCase);

    [GeneratedRegex(@"(?<!\p{L})(?:€|eur\s*)?(?<amount>\d+(?:[\.,]\d{1,2})?)(?!\d)", RegexOptions.IgnoreCase)]
    private static partial Regex AmountRegex();
    
   [GeneratedRegex( @"\b(?:using|with)\s+(?<account>aib|revolut|cash)\b",RegexOptions.IgnoreCase)]
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

    [GeneratedRegex(@"\s+and\s+(?=(?:€|eur\s*)?\d)",RegexOptions.IgnoreCase)]
    private static partial Regex TransactionSeparatorRegex();
}
