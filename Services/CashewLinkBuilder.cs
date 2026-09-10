using System.Globalization;
using MoneyPing.Models;

namespace MoneyPing.Services;

public sealed class CashewLinkBuilder
{
    private const string BaseUrl = "https://cashewapp.web.app/addTransaction";

    public string Build(ParsedTransaction transaction)
    {
        var query = new Dictionary<string, string>
        {
            ["account"] = transaction.Account ?? "Aib",
            ["amount"] = transaction.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            ["title"] = transaction.Title,
            ["date"] = transaction.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrWhiteSpace(transaction.Category))
            query["category"] = transaction.Category;

        if (!string.IsNullOrWhiteSpace(transaction.Account))
            query["account"] = transaction.Account;

        if (!string.IsNullOrWhiteSpace(transaction.Notes))
            query["notes"] = transaction.Notes;

        var encoded = string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return $"{BaseUrl}?{encoded}";
    }
}
