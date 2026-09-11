using MoneyPing.Models;

namespace MoneyPing.Services;

public sealed class RecurrenceScheduleGenerator
{
    private const int MaxOccurrences = 500;

    public IReadOnlyList<ParsedTransaction> Generate(
        ParsedTransaction source)
    {
        var recurrence = source.Recurrence
            ?? throw new InvalidOperationException(
                "Transaction is not recurring.");

        if (!recurrence.IsBounded)
        {
            throw new InvalidOperationException(
                "Cannot materialize an unbounded recurrence.");
        }

        var startDate =
            DateOnly.FromDateTime(source.Date);

        DateOnly? endExclusive = null;

        if (recurrence.Duration is not null)
        {
            endExclusive = Add(
                startDate,
                recurrence.Duration.Value,
                recurrence.Duration.Unit);
        }

        var transactions =
            new List<ParsedTransaction>();

        for (
            var index = 0;
            index < MaxOccurrences;
            index++)
        {
            if (recurrence.OccurrenceCount.HasValue &&
                index >= recurrence.OccurrenceCount.Value)
            {
                break;
            }

            var occurrenceDate = Add(
                startDate,
                recurrence.Interval * index,
                recurrence.Unit);

            if (recurrence.UntilDate.HasValue &&
                occurrenceDate > recurrence.UntilDate.Value)
            {
                break;
            }

            if (endExclusive.HasValue &&
                occurrenceDate >= endExclusive.Value)
            {
                break;
            }

            transactions.Add(
                CreateOccurrence(
                    source,
                    occurrenceDate));
        }

        if (transactions.Count == MaxOccurrences)
        {
            throw new InvalidOperationException(
                "Recurrence generated too many transactions.");
        }

        return transactions;
    }

    private static ParsedTransaction CreateOccurrence(
        ParsedTransaction source,
        DateOnly date)
    {
        return new ParsedTransaction
        {
            Amount = source.Amount,
            Title = source.Title,
            Category = source.Category,
            Subcategory = source.Subcategory,
            Account = source.Account,

            Date = date.ToDateTime(
                TimeOnly.MinValue),

            Notes = source.Notes,

            Recurrence = null
        };
    }

    private static DateOnly Add(
        DateOnly date,
        int value,
        RecurrenceUnit unit)
    {
        return unit switch
        {
            RecurrenceUnit.Day =>
                date.AddDays(value),

            RecurrenceUnit.Week =>
                date.AddDays(value * 7),

            RecurrenceUnit.Month =>
                date.AddMonths(value),

            RecurrenceUnit.Year =>
                date.AddYears(value),

            _ => throw new ArgumentOutOfRangeException(
                nameof(unit))
        };
    }
}