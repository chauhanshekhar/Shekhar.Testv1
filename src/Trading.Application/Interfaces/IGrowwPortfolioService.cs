using Trading.Application.Models;

namespace Trading.Application.Interfaces;

public interface IGrowwPortfolioService
{
    Task<List<HoldingEntry>> GetHoldingsAsync();
    Task<List<PositionEntry>> GetPositionsAsync(string? segment = null);
    Task<PositionEntry?> GetPositionAsync(string tradingSymbol, string? segment = null);
}
