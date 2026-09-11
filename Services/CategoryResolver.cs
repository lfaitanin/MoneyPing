using System.Text.Json;
using MoneyPing.Models;

namespace MoneyPing.Services;

public sealed class CategoryResolver
{
    private readonly Dictionary<string, TransactionClassification> _rules;

    public CategoryResolver(string rulesPath)
    {
       var json = File.ReadAllText(rulesPath);

        _rules =
            JsonSerializer.Deserialize<
                Dictionary<string, TransactionClassification>
            >(json)
            ?? [];
    }

    public TransactionClassification? Resolve(string title)
    {
        var normalizedTitle =
            title.Trim().ToLowerInvariant();

        foreach (var rule in _rules)
        {
            if (normalizedTitle.Contains(
                    rule.Key.ToLowerInvariant()))
            {
                return rule.Value;
            }
        }

        return null;
    }
}
