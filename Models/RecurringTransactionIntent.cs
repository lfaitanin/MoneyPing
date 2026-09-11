namespace MoneyPing.Models;

public sealed class RecurringTransactionIntent
{
    public int Interval { get; init; } = 1;

    public RecurrenceUnit Unit { get; init; }
    public DateOnly? StartDate { get; init; }
    public int? OccurrenceCount { get; init; }
    public DateOnly? UntilDate { get; init; }
    public RecurrenceDuration? Duration { get; init; }
    public bool IsBounded => OccurrenceCount.HasValue || UntilDate.HasValue || Duration is not null;
    public override string ToString()
    {
        var unit = Unit.ToString();

        if (Interval > 1)
            unit += "s";

        return Interval == 1
            ? $"Every {unit.ToLowerInvariant()}"
            : $"Every {Interval} {unit.ToLowerInvariant()}";
    }
    public sealed class RecurrenceDuration
    {
        public int Value { get; init; }
        public RecurrenceUnit Unit { get; init; }
    }
}