using System.Text.Json;

namespace MoneyPing.Services;

public sealed class CategoryResolver
{
    private readonly Dictionary<string, string> _rules;

    public CategoryResolver(string rulesPath)
    {
        if (!File.Exists(rulesPath))
        {
            _rules = new(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var json = File.ReadAllText(rulesPath);
        _rules = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                 ?? new Dictionary<string, string>();
    }

    public string? Resolve(string title)
    {
        var normalizedTitle = title.Trim().ToLowerInvariant();

        return _rules
            .OrderByDescending(pair => pair.Key.Length)
            .FirstOrDefault(pair => normalizedTitle.Contains(pair.Key.ToLowerInvariant()))
            .Value;
    }
}
