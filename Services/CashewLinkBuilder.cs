using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using MoneyPing.Models;

namespace MoneyPing.Services;

public sealed class CashewLinkBuilder
{
    private const string AddTransactionUrl = "https://cashewapp.web.app/addTransaction";
    private const string AddTransactionRouteUrl = "https://cashewapp.web.app/addTransactionRoute";

    
    public string BuildUrl(ParsedTransaction transaction, string? baseUrl)
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

        if (!string.IsNullOrWhiteSpace(transaction.Subcategory))
            query["subcategory"] = transaction.Subcategory;

        if (!string.IsNullOrWhiteSpace(transaction.Account))
            query["account"] = transaction.Account;

        if (!string.IsNullOrWhiteSpace(transaction.Notes))
            query["notes"] = transaction.Notes;

        var encoded = string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

        return $"{baseUrl}?{encoded}";
    }
    public string BuildMany(
    IEnumerable<ParsedTransaction> transactions)
    {
        var payload = new
        {
            transactions = transactions.Select(transaction => new
            {
                amount = transaction.Amount.ToString(CultureInfo.InvariantCulture),
                title = transaction.Title,
                notes = transaction.Notes,
                date = transaction.Date.ToString("yyyy-MM-dd"),
                category = transaction.Category,
                subcategory = transaction.Subcategory,
                account = transaction.Account
            }).ToList()
        };

        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition =
                JsonIgnoreCondition.WhenWritingNull
        };

        var json = JsonSerializer.Serialize(
            payload,
            options);
            
        var encodedJson =
            Uri.EscapeDataString(json);

        return $"{AddTransactionUrl}?JSON={encodedJson}";
    }
    public string BuildTransfer(TransferIntent transfer)
    {
        var transactions =
            new List<ParsedTransaction>
            {
                new()
                {
                    Amount = -Math.Abs(transfer.Amount),
                    Title =$"Transfer to {transfer.DestinationAccount}",
                    Category = "Balance Correction",
                    Account = transfer.SourceAccount,
                    Date = transfer.Date,
                    Notes = transfer.Notes
                },

                new()
                {
                    Amount = Math.Abs(transfer.Amount),
                    Title =$"Transfer from {transfer.SourceAccount}",
                    Category = "Balance Correction",
                    Account = transfer.DestinationAccount,
                    Date = transfer.Date,
                    Notes = transfer.Notes
                }
            };

        return BuildMany(transactions);
    }
    public string BuildRoute(ParsedTransaction transaction)
    {
        return BuildUrl(
            transaction,
            AddTransactionRouteUrl);
    }
    public string Build(
    ParsedTransaction transaction)
    {
        return BuildUrl(
        transaction,
        AddTransactionUrl);
    }
}
