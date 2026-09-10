using MoneyPing.Models;

namespace MoneyPing.Services;

public interface ITransactionParser
{
    ParseResult Parse(string input);
}