using System.Text.RegularExpressions;

namespace MoneyPing.Services;

public sealed partial class AdvancedIntentDetector
{
    private static readonly string[] RecurrenceHints =
    [
        "every ",
        "daily",
        "weekly",
        "monthly",
        "yearly",
        "annually",
        "recurring",
        "subscription",
        "renews",
        "renewal",
        "until ",
        "for the next ",
        "for the following "
    ];

    private static readonly string[] DateWords =
    [
        "tomorrow",

        "monday",
        "tuesday",
        "wednesday",
        "thursday",
        "friday",
        "saturday",
        "sunday",

        "january",
        "february",
        "march",
        "april",
        "may",
        "june",
        "july",
        "august",
        "september",
        "october",
        "november",
        "december",

        "next week",
        "next month",
        "next year",

        "last week",
        "last month",
        "last year",

        "days ago",
        "weeks ago",
        "months ago",
        "years ago",

        "in a week",
        "in a month",
        "in a year",

        "starting ",
        "until "
    ];

    public bool RequiresAdvancedParsing(string input)
    {
        var normalized = input.ToLowerInvariant();

        if (RecurrenceHints.Any(normalized.Contains))
            return true;

        if (DateWords.Any(normalized.Contains))
            return true;

        if (IsoDateRegex().IsMatch(normalized))
            return true;

        if (NumericDateRegex().IsMatch(normalized))
            return true;

        return false;
    }

    [GeneratedRegex(
        @"\b\d{4}-\d{2}-\d{2}\b")]
    private static partial Regex IsoDateRegex();

    [GeneratedRegex(
        @"\b\d{1,2}[/-]\d{1,2}(?:[/-]\d{2,4})?\b")]
    private static partial Regex NumericDateRegex();
}