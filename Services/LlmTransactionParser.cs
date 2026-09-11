using System.Text.Json;
using MoneyPing.Models;
using OpenAI.Chat;

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
                    - title should contain only the merchant or income source.
                    - account should contain the payment account.
                    - Resolve relative dates using today's date: {DateTime.Today:yyyy-MM-dd}.
                    - Do not invent transactions.
                    - Do not categorize transactions.
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
                                      }
                                    },
                                    "required": [
                                      "type",
                                      "amount",
                                      "title",
                                      "account",
                                      "date"
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

            var transactions =
                new List<ParsedTransaction>();

            foreach (var item in response.Transactions)
            {
                if (item.Amount <= 0)
                {
                    return ParseResult.Fail(
                        "AI parser returned an invalid amount.");
                }

                if (!DateTime.TryParse(
                        item.Date,
                        out var date))
                {
                    return ParseResult.Fail(
                        "AI parser returned an invalid date.");
                }

                var amount =
                    item.Type == "income"
                        ? Math.Abs(item.Amount)
                        : -Math.Abs(item.Amount);

                transactions.Add(
                    new ParsedTransaction
                    {
                        Amount = amount,
                        Title = item.Title,
                        Account = item.Account,
                        Date = date,
                        Notes = input.Trim()
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
}