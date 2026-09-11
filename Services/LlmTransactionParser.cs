using System.Globalization;
using System.Text.Json;
using MoneyPing.Models;
using OpenAI.Chat;
using static MoneyPing.Models.RecurringTransactionIntent;

namespace MoneyPing.Services;

public sealed class LlmTransactionParser : ITransactionParser
{
    private readonly ChatClient _client;

    public LlmTransactionParser(
        string apiKey,
        string model)
    {
        _client = new ChatClient(
            model: model,
            apiKey: apiKey);
    }

    public async Task<ParseResult> ParseAsync(
        string input,
        CancellationToken cancellationToken = default)
    {
        try
        {
            List<ChatMessage> messages =
            [
                new SystemChatMessage(
                $"""
                You are the transaction parser for MoneyPing.

                Convert the user's message into structured financial transactions.

                Rules:
                - Return one item for each financial transaction.
                - type must be either "expense" or "income".
                - amount must always be positive.
                - title must contain only the merchant or income source.
                - account must contain only the financial account.
                - Resolve relative dates using today's date: {DateTime.Today:yyyy-MM-dd}.
                - Dates must use yyyy-MM-dd.
                - Do not invent transactions.
                - Do not categorize transactions.

                Recurrence:
                - recurrence must be null for non-recurring transactions.
                - interval must be a positive integer.
                - unit must be one of: day, week, month, year.
                - "monthly" means interval 1, unit month.
                - "weekly" means interval 1, unit week.
                - "every 4 weeks" means interval 4, unit week.
                - "yearly" or "annually" means interval 1, unit year.

                Recurrence boundaries:
                - "for 10 payments" => occurrenceCount = 10.
                - "for 6 months" => duration.value = 6, duration.unit = month.
                - "until March 2027" => resolve untilDate to an ISO date.
                - If no ending condition is provided, occurrenceCount, untilDate and duration must all be null.
                - Do not calculate or return every future payment date.
                MoneyPing will calculate the schedule deterministically.
                
                Date rules:
                - Resolve all relative dates into yyyy-MM-dd.
                - "tomorrow" means the next calendar day.
                - "next Friday" means the next Friday strictly after today.
                - "last Friday" means the most recent Friday before today.
                - "in two weeks" means 14 calendar days after today.
                - "two days ago" means two calendar days before today.
                - If no date is provided, use today's date.
                - Do not return relative date expressions. Always return yyyy-MM-dd.
                """),
                new UserChatMessage(input)
            ];

            var options = new ChatCompletionOptions
            {
                ResponseFormat =
                    ChatResponseFormat.CreateJsonSchemaFormat(
                        jsonSchemaFormatName: "moneyping_transactions",

                        jsonSchema: BinaryData.FromBytes(
                            """
                            {
                              "type": "object",
                              "properties": {
                                "transactions": {
                                  "type": "array",
                                  "items": {
                                    "type": "object",
                                    "properties": {
                                      "type": {
                                        "type": "string",
                                        "enum": ["expense", "income"]
                                      },
                                      "amount": {
                                        "type": "number"
                                      },
                                      "title": {
                                        "type": "string"
                                      },
                                      "account": {
                                        "type": "string"
                                      },
                                      "date": {
                                        "type": "string"
                                      },
                                      "recurrence": {
                                        "anyOf": [
                                            {
                                            "type": "object",
                                            "properties": {
                                                "interval": {
                                                "type": "integer",
                                                "minimum": 1
                                                },
                                                "unit": {
                                                "type": "string",
                                                "enum": [
                                                    "day",
                                                    "week",
                                                    "month",
                                                    "year"
                                                ]
                                                },
                                                "occurrenceCount": {
                                                "anyOf": [
                                                    {
                                                    "type": "integer",
                                                    "minimum": 1
                                                    },
                                                    {
                                                    "type": "null"
                                                    }
                                                ]
                                                },
                                                "untilDate": {
                                                "anyOf": [
                                                    {
                                                    "type": "string"
                                                    },
                                                    {
                                                    "type": "null"
                                                    }
                                                ]
                                                },
                                                "duration": {
                                                "anyOf": [
                                                    {
                                                    "type": "object",
                                                    "properties": {
                                                        "value": {
                                                        "type": "integer",
                                                        "minimum": 1
                                                        },
                                                        "unit": {
                                                        "type": "string",
                                                        "enum": [
                                                            "day",
                                                            "week",
                                                            "month",
                                                            "year"
                                                        ]
                                                        }
                                                    },
                                                    "required": [
                                                        "value",
                                                        "unit"
                                                    ],
                                                    "additionalProperties": false
                                                    },
                                                    {
                                                    "type": "null"
                                                    }
                                                ]
                                                }
                                            },
                                            "required": [
                                                "interval",
                                                "unit",
                                                "occurrenceCount",
                                                "untilDate",
                                                "duration"
                                            ],
                                            "additionalProperties": false
                                            },
                                            {
                                            "type": "null"
                                            }
                                        ]
                                        }
                                    },
                                    "required": [
                                      "type",
                                      "amount",
                                      "title",
                                      "account",
                                      "date",
                                      "recurrence"
                                    ],
                                    "additionalProperties": false
                                  }
                                }
                              },
                              "required": ["transactions"],
                              "additionalProperties": false
                            }
                            """u8.ToArray()),

                        jsonSchemaIsStrict: true)
            };

            var completion =
                await _client.CompleteChatAsync(
                    messages,
                    options,
                    cancellationToken);

            var json = completion.Value.Content[0].Text;

            var response =
                JsonSerializer.Deserialize<LlmTransactionResponse>(
                    json);

            if (response is null ||
                response.Transactions.Count == 0)
            {
                return ParseResult.Fail(
                    "AI parser did not find any transactions.");
            }

            var transactions = new List<ParsedTransaction>();

            foreach (var item in response.Transactions)
            {
                if (item.Amount <= 0)
                {
                    return ParseResult.Fail(
                        "AI parser returned an invalid amount.");
                }

                if (!DateTime.TryParseExact(
                        item.Date,
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var date))
                {
                    return ParseResult.Fail(
                        "AI parser returned an invalid date.");
                }

                var amount =
                    item.Type == "income"
                        ? Math.Abs(item.Amount)
                        : -Math.Abs(item.Amount);

                RecurringTransactionIntent? recurrence = null;

                if (item.Recurrence is not null)
                {
                    var unit = ParseRecurrenceUnit(
                        item.Recurrence.Unit);

                    RecurrenceDuration? duration = null;

                    if (item.Recurrence.Duration is not null)
                    {
                        duration = new RecurrenceDuration
                        {
                            Value = item.Recurrence.Duration.Value,
                            Unit = ParseRecurrenceUnit(
                                item.Recurrence.Duration.Unit)
                        };
                    }

                    DateOnly? untilDate = null;

                    if (!string.IsNullOrWhiteSpace(
                            item.Recurrence.UntilDate))
                    {
                        if (!DateOnly.TryParseExact(
                                item.Recurrence.UntilDate,
                                "yyyy-MM-dd",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out var parsedUntilDate))
                        {
                            return ParseResult.Fail(
                                "AI parser returned an invalid recurrence end date.");
                        }

                        untilDate = parsedUntilDate;
                    }

                    recurrence = new RecurringTransactionIntent
                    {
                        Interval = item.Recurrence.Interval,
                        Unit = unit,
                        OccurrenceCount = item.Recurrence.OccurrenceCount,
                        UntilDate = untilDate,
                        Duration = duration,
                    };
                    var boundaries = 0;

                    if (item.Recurrence.OccurrenceCount.HasValue)
                        boundaries++;

                    if (!string.IsNullOrWhiteSpace(item.Recurrence.UntilDate))
                        boundaries++;

                    if (item.Recurrence.Duration is not null)
                        boundaries++;

                    if (boundaries > 1)
                    {
                        return ParseResult.Fail(
                            "AI parser returned multiple recurrence boundaries.");
                    }
                }
                var title = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(item.Title.Trim().ToLowerInvariant());

                transactions.Add(new ParsedTransaction
                    {
                        Amount = amount,
                        Title = title,
                        Account = item.Account,
                        Date = date,
                        Notes = input.Trim(),
                        Recurrence = recurrence
                    });
            }

            return ParseResult.Ok(transactions);
        }
        catch (Exception ex)
        {
            return ParseResult.Fail(
                $"AI parser failed: {ex.Message}");
        }
    }
    private static RecurrenceUnit ParseRecurrenceUnit( string unit)
    {
        return unit.ToLowerInvariant() switch
        {
            "day" => RecurrenceUnit.Day,
            "week" => RecurrenceUnit.Week,
            "month" => RecurrenceUnit.Month,
            "year" => RecurrenceUnit.Year,

            _ => throw new InvalidOperationException(
                $"Unsupported recurrence unit: {unit}")
        };
    }
}