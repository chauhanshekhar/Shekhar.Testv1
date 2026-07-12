using Trading.Application.Models;
using Trading.Application.Interfaces;

namespace Trading.Infrastructure.Brokers.Groww;

// Holdings (DEMAT) and positions (intraday/carry-forward) via Groww Trade API.
public class GrowwPortfolioService : IGrowwPortfolioService
{
    private readonly IGrowwHttpService _growwHttp;

    public GrowwPortfolioService(IGrowwHttpService growwHttp)
    {
        _growwHttp = growwHttp;
    }

    // Holdings stored in the user's DEMAT account.
    public async Task<List<HoldingEntry>> GetHoldingsAsync()
    {
        var result = await _growwHttp.GetAsync<GrowwResponse<HoldingsPayload>>(GrowwEndpoint.Holdings);
        return result.Data?.Payload?.Holdings ?? [];
    }

    // All positions for the current trading day (optional segment filter).
    public async Task<List<PositionEntry>> GetPositionsAsync(string? segment = null)
    {
        var url = GrowwEndpoint.Positions;
        if (!string.IsNullOrEmpty(segment))
            url += $"?segment={Uri.EscapeDataString(segment)}";

        var result = await _growwHttp.GetAsync<GrowwResponse<PositionsPayload>>(url);
        return result.Data?.Payload?.Positions ?? [];
    }

    // Position for a specific trading symbol.
    public async Task<PositionEntry?> GetPositionAsync(string tradingSymbol, string? segment = null)
    {
        var url = $"{GrowwEndpoint.PositionBySymbol}?trading_symbol={Uri.EscapeDataString(tradingSymbol)}";
        if (!string.IsNullOrEmpty(segment))
            url += $"&segment={Uri.EscapeDataString(segment)}";

        var result = await _growwHttp.GetAsync<GrowwResponse<PositionEntry>>(url);
        return result.Data?.Payload;
    }
}
