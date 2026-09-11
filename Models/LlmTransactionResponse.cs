using System.Text.Json.Serialization;

namespace MoneyPing.Models;

public sealed class LlmTransactionResponse
{
    [JsonPropertyName("transactions")]
    public List<LlmTransaction> Transactions { get; init; } = [];
}

public sealed class LlmTransaction
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("title")]
    public required string Title { get; init; }

    [JsonPropertyName("account")]
    public required string Account { get; init; }

    [JsonPropertyName("date")]
    public required string Date { get; init; }
}