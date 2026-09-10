using MoneyPing.Services;
using Xunit;
namespace ExpenseAgent.Tests;

public sealed class TransactionParserTests
{
    private readonly TransactionParser _parser = new();

    [Fact]
    public void Should_Parse_Expense()
    {
        var result = _parser.Parse(
            "spent 25 at Lidl using Aib");

        Assert.True(
            result.Success,
            $"Parser failed: {result.Error}");
        
        var transaction = result.Transaction!;

        Assert.Equal(-25m, transaction.Amount);
        Assert.Equal("Lidl", transaction.Title);
    }

    [Fact]
    public void Should_Parse_Income()
    {
        var result = _parser.Parse("received 300 from TK Maxx using AIB");

        Assert.True(
            result.Success,
            $"Parser failed: {result.Error}");
        var transaction = result.Transaction!;

        Assert.Equal(300m, transaction.Amount);
        Assert.Equal("From Tk Maxx", transaction.Title);
    }

    [Fact]
    public void Should_Reject_Message_Without_Amount()
    {
        var result = _parser.Parse(
            "spent money at Lidl");

        Assert.False(result.Success);
    }

    [Fact]
    public void Should_Reject_Message_Without_Transaction_Type()
    {
        var result = _parser.Parse(
            "25 at Lidl using AIB");

        Assert.False(result.Success);
    }

    [Fact]
    public void Should_Parse_Yesterday()
    {
        var result = _parser.Parse(
            "spent 10 at Lidl yesterday using AIB");

        Assert.True(result.Success);

        Assert.Equal(
            DateTime.Today.AddDays(-1),
            result.Transaction!.Date);
    }
}