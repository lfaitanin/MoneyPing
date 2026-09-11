using MoneyPing.Models;
using MoneyPing.Services;
using Xunit;

namespace MoneyPing.Tests;

public sealed class HybridTransactionParserTests
{
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

        var primary = new FakeTransactionParser(primaryResult);

        var fallback = new FakeTransactionParser(
            ParseResult.Fail("Fallback should not be called."));

        var parser = new HybridTransactionParser(
            primary,
            fallback);

        var result = await parser.ParseAsync(
            "spent 25 at Lidl using AIB");

        Assert.True(result.Success);

        Assert.True(primary.WasCalled);
        Assert.False(fallback.WasCalled);

        Assert.Single(result.Transactions);
        Assert.Equal("Lidl", result.Transactions[0].Title);
    }

    [Fact]
    public async Task Should_Use_Fallback_When_Primary_Returns_Unsupported()
    {
        var primary = new FakeTransactionParser(
            ParseResult.Unsupported(
                "Message is too complex for rule-based parsing."));

        var fallbackResult = ParseResult.Ok(
            new ParsedTransaction
            {
                Amount = -25m,
                Title = "Lidl",
                Account = "AIB",
                Date = DateTime.Today
            });

        var fallback =
            new FakeTransactionParser(fallbackResult);

        var parser = new HybridTransactionParser(
            primary,
            fallback);

        var result = await parser.ParseAsync(
            "yesterday after work I bought groceries at Lidl for 25 euros");

        Assert.True(result.Success);

        Assert.True(primary.WasCalled);
        Assert.True(fallback.WasCalled);

        Assert.Single(result.Transactions);
        Assert.Equal("Lidl", result.Transactions[0].Title);
    }

    [Fact]
    public async Task Should_Not_Use_Fallback_When_Primary_Returns_Validation_Error()
    {
        var primary = new FakeTransactionParser(
            ParseResult.Fail(
                "Was this an expense or income?"));

        var fallback = new FakeTransactionParser(
            ParseResult.Ok(
                new ParsedTransaction
                {
                    Amount = -25m,
                    Title = "Should Not Be Used",
                    Account = "AIB",
                    Date = DateTime.Today
                }));

        var parser = new HybridTransactionParser(
            primary,
            fallback);

        var result = await parser.ParseAsync(
            "25 at Lidl using AIB");

        Assert.False(result.Success);

        Assert.True(primary.WasCalled);
        Assert.False(fallback.WasCalled);
    }


    private sealed class FakeTransactionParser : ITransactionParser
    {
        private readonly ParseResult _result;

        public bool WasCalled { get; private set; }

        public FakeTransactionParser(ParseResult result)
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