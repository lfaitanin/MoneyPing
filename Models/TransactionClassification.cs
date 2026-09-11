using System.Text.Json.Serialization;

namespace MoneyPing.Models;

public sealed class TransactionClassification
{
    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonPropertyName("subcategory")]
    public string? Subcategory { get; init; }
}