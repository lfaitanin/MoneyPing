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

    [JsonPropertyName("recurrence")]
    public LlmRecurrence? Recurrence { get; init; }
}

public sealed class LlmRecurrence
{
    [JsonPropertyName("interval")]
    public int Interval { get; init; }

    [JsonPropertyName("unit")]
    public required string Unit { get; init; }

    [JsonPropertyName("occurrenceCount")]
    public int? OccurrenceCount { get; init; }

    [JsonPropertyName("untilDate")]
    public string? UntilDate { get; init; }

    [JsonPropertyName("duration")]
    public LlmRecurrenceDuration? Duration { get; init; }
}

public sealed class LlmRecurrenceDuration
{
    [JsonPropertyName("value")]
    public int Value { get; init; }

    [JsonPropertyName("unit")]
    public required string Unit { get; init; }
}