using Trading.Application.Models;

namespace Trading.Application.Interfaces;

public interface IGrowwLiveDataService
{
    Task<GrowwQuotePayload?> GetQuoteAsync(string exchange, string segment, string tradingSymbol);
    Task<Dictionary<string, decimal>> GetLtpAsync(string segment, IEnumerable<string> exchangeSymbols);
    Task<Dictionary<string, OhlcEntry>> GetOhlcAsync(string segment, IEnumerable<string> exchangeSymbols);
    Task<OptionChainPayload?> GetOptionChainAsync(string exchange, string underlying, string expiryDate);
    Task<GreeksPayload?> GetGreeksAsync(string exchange, string underlying, string tradingSymbol, string expiry);
}
