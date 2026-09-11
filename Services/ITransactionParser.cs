using MoneyPing.Models;

namespace MoneyPing.Services;
public interface ITransactionParser
{
    Task<ParseResult> ParseAsync(
        string input,
        CancellationToken cancellationToken = default);
}