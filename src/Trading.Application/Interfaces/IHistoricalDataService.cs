using Trading.Domain.Entities;

namespace Trading.Application.Interfaces;

public interface IHistoricalDataService
{
    Task<List<Candle>> GetCandlesAsync(
        string exchange,
        string token,
        string interval,
        DateTime from,
        DateTime to,
        string segment = "CASH");


    Task<List<string>> GetContractsAsync(string exchange, string underlyingSymbol, string expiryDate);
}
