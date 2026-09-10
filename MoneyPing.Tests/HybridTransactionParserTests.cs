using MoneyPing.Models;
using MoneyPing.Services;
using Xunit;

namespace MoneyPing.Tests;

public sealed class HybridTransactionParserTests
{
    [Fact]
    public void Should_Use_Primary_Parser_When_It_Succeeds()
    {
        var primary = new SuccessfulParser();
        var fallback = new FailingParser();

        var parser = new HybridTransactionParser(
            primary,
            fallback);

        var result = parser.Parse("anything");

        Assert.True(result.Success);
        Assert.Single(result.Transactions);
        Assert.Equal("Primary", result.Transactions[0].Title);
    }
    [Fact]
    public void Should_Use_Fallback_Parser_When_Primary_Fails()
    {
        var primary = new FailingParser();
        var fallback = new SuccessfulParser();

        var parser = new HybridTransactionParser(
            primary,
            fallback);

        var result = parser.Parse("complex message");

        Assert.True(result.Success);
        Assert.Single(result.Transactions);
        Assert.Equal("Primary", result.Transactions[0].Title);
    }
    private sealed class SuccessfulParser : ITransactionParser
    {
        public ParseResult Parse(string input)
        {
            return ParseResult.Ok(
                new ParsedTransaction
                {
                    Amount = -10,
                    Title = "Primary",
                    Date = DateTime.Today
                });
        }
    }

    private sealed class FailingParser : ITransactionParser
    {
        public ParseResult Parse(string input)
        {
            return ParseResult.Fail("Failed");
        }
    }
}