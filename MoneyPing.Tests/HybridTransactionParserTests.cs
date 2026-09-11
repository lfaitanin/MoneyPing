using MoneyPing.Models;
using MoneyPing.Services;
using Xunit;
using static MoneyPing.Models.RecurringTransactionIntent;

namespace MoneyPing.Tests;

public sealed class HybridTransactionParserTests
{
    private readonly AdvancedIntentDetector _intentDetector = new();

    [Fact]
    public async Task Should_Return_Primary_Result_When_Primary_Succeeds()
    {
        var primaryResult = ParseResult.Ok(
            new ParsedTransaction
            {
                Amount = -25m,
                Title = "Lidl",
                Account = "AIB",
                Date = DateTime.Today
            });

        var primary =
            new FakeTransactionParser(primaryResult);

        var fallback =
            new FakeTransactionParser(
                ParseResult.Fail(
                    "Fallback should not be called."));

        var parser =
            new HybridTransactionParser(
                primary,
                fallback,
                _intentDetector);

        var result = await parser.ParseAsync(
            "spent 25 at Lidl using AIB");

        Assert.True(result.Success);

        Assert.True(primary.WasCalled);
        Assert.False(fallback.WasCalled);

        var transaction =
            Assert.Single(result.Transactions);

        Assert.Equal("Lidl", transaction.Title);
        Assert.Equal(-25m, transaction.Amount);
    }

    [Fact]
    public async Task Should_Use_Fallback_When_Primary_Returns_Unsupported()
    {
        var primary =
            new FakeTransactionParser(
                ParseResult.Unsupported(
                    "Message is too complex for rule-based parsing."));

        var fallbackResult =
            ParseResult.Ok(
                new ParsedTransaction
                {
                    Amount = -25m,
                    Title = "Lidl",
                    Account = "AIB",
                    Date = DateTime.Today
                });

        var fallback =
            new FakeTransactionParser(
                fallbackResult);

        var parser =
            new HybridTransactionParser(
                primary,
                fallback,
                _intentDetector);

        var result = await parser.ParseAsync(
            "after work I bought groceries at Lidl for 25 euros");

        Assert.True(result.Success);

        Assert.True(primary.WasCalled);
        Assert.True(fallback.WasCalled);

        var transaction =
            Assert.Single(result.Transactions);

        Assert.Equal("Lidl", transaction.Title);
    }

    [Fact]
    public async Task Should_Not_Use_Fallback_When_Primary_Returns_Validation_Error()
    {
        var primary =
            new FakeTransactionParser(
                ParseResult.Fail(
                    "Was this an expense or income?"));

        var fallback =
            new FakeTransactionParser(
                ParseResult.Ok(
                    new ParsedTransaction
                    {
                        Amount = -25m,
                        Title = "Should Not Be Used",
                        Account = "AIB",
                        Date = DateTime.Today
                    }));

        var parser =
            new HybridTransactionParser(
                primary,
                fallback,
                _intentDetector);

        var result = await parser.ParseAsync(
            "25 at Lidl using AIB");

        Assert.False(result.Success);

        Assert.True(primary.WasCalled);
        Assert.False(fallback.WasCalled);

        Assert.Equal(
            ParseFailureKind.Validation,
            result.FailureKind);
    }

    [Fact]
    public async Task Should_Bypass_Primary_For_Recurring_Transaction()
    {
        var primary =
            new FakeTransactionParser(
                ParseResult.Fail(
                    "Primary should not be called."));

        var fallback =
            new FakeTransactionParser(
                ParseResult.Ok(
                    new ParsedTransaction
                    {
                        Amount = -650m,
                        Title = "Rent",
                        Account = "AIB",
                        Date = DateTime.Today,
                        Recurrence =
                            new RecurringTransactionIntent
                            {
                                Interval = 4,
                                Unit = RecurrenceUnit.Week,

                                Duration =
                                    new RecurrenceDuration
                                    {
                                        Value = 6,
                                        Unit = RecurrenceUnit.Month
                                    }
                            }
                    }));

        var parser =
            new HybridTransactionParser(
                primary,
                fallback,
                _intentDetector);

        var result = await parser.ParseAsync(
            "paid 650 rent using AIB every 4 weeks for 6 months");

        Assert.True(result.Success);

        // This is the important part:
        Assert.False(primary.WasCalled);
        Assert.True(fallback.WasCalled);

        var transaction =
            Assert.Single(result.Transactions);

        Assert.NotNull(transaction.Recurrence);

        Assert.Equal(
            4,
            transaction.Recurrence.Interval);

        Assert.Equal(
            RecurrenceUnit.Week,
            transaction.Recurrence.Unit);

        Assert.NotNull(
            transaction.Recurrence.Duration);
        Assert.Equal(
            6,
            transaction.Recurrence.Duration.Value);
        Assert.Equal(
            RecurrenceUnit.Month,
            transaction.Recurrence.Duration.Unit);
    }

    [Fact]
    public async Task Should_Bypass_Primary_For_Advanced_Date()
    {
        var primary =
            new FakeTransactionParser(
                ParseResult.Fail(
                    "Primary should not be called."));

        var expectedDate =
            new DateTime(2026, 9, 18);

        var fallback =
            new FakeTransactionParser(
                ParseResult.Ok(
                    new ParsedTransaction
                    {
                        Amount = -25m,
                        Title = "Lidl",
                        Account = "AIB",
                        Date = expectedDate
                    }));

        var parser =
            new HybridTransactionParser(
                primary,
                fallback,
                _intentDetector);

        var result = await parser.ParseAsync(
            "spent 25 at Lidl next Friday using AIB");

        Assert.True(result.Success);

        Assert.False(primary.WasCalled);
        Assert.True(fallback.WasCalled);

        var transaction =
            Assert.Single(result.Transactions);

        Assert.Equal(
            expectedDate,
            transaction.Date);
    }


    private sealed class FakeTransactionParser
        : ITransactionParser
    {
        private readonly ParseResult _result;

        public bool WasCalled { get; private set; }

        public FakeTransactionParser(
            ParseResult result)
        {
            _result = result;
        }

        public Task<ParseResult> ParseAsync(
            string input,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;

            return Task.FromResult(_result);
        }
    }
}