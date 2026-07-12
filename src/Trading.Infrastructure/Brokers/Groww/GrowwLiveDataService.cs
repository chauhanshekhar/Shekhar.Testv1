using Trading.Application.Models;
using Trading.Application.Interfaces;

namespace Trading.Infrastructure.Brokers.Groww;

// Provides live market data: quote, LTP batch, OHLC batch, option chain, Greeks.
public class GrowwLiveDataService : IGrowwLiveDataService
{
    private readonly IGrowwHttpService _growwHttp;

    public GrowwLiveDataService(IGrowwHttpService growwHttp)
    {
        _growwHttp = growwHttp;
    }

    // Full quote snapshot for a single instrument (OHLCV, depth, circuit limits).
    public async Task<GrowwQuotePayload?> GetQuoteAsync(
        string exchange,
        string segment,
        string tradingSymbol)
    {
        var url = GrowwEndpoint.LiveQuote +
                  $"?exchange={Uri.EscapeDataString(exchange)}" +
                  $"&segment={Uri.EscapeDataString(segment)}" +
                  $"&trading_symbol={Uri.EscapeDataString(tradingSymbol)}";

        var result = await _growwHttp.GetAsync<GrowwResponse<GrowwQuotePayload>>(url);
        return result.Data?.Payload;
    }

    // Batch LTP for up to 50 symbols. exchangeSymbols = ["NSE:NIFTY", "NSE:BANKNIFTY"].
    public async Task<Dictionary<string, decimal>> GetLtpAsync(
        string segment,
        IEnumerable<string> exchangeSymbols)
    {
        var queryParams = string.Join("&",
            exchangeSymbols.Select(s => $"exchange_symbols={Uri.EscapeDataString(s)}"));

        var url = $"{GrowwEndpoint.LiveLtp}?segment={Uri.EscapeDataString(segment)}&{queryParams}";

        var result = await _growwHttp.GetAsync<GrowwResponse<Dictionary<string, decimal>>>(url);
        return result.Data?.Payload ?? [];
    }

    // Batch OHLC for up to 50 symbols.
    public async Task<Dictionary<string, OhlcEntry>> GetOhlcAsync(
        string segment,
        IEnumerable<string> exchangeSymbols)
    {
        var queryParams = string.Join("&",
            exchangeSymbols.Select(s => $"exchange_symbols={Uri.EscapeDataString(s)}"));

        var url = $"{GrowwEndpoint.LiveOhlc}?segment={Uri.EscapeDataString(segment)}&{queryParams}";

        var result = await _growwHttp.GetAsync<GrowwResponse<Dictionary<string, OhlcEntry>>>(url);
        return result.Data?.Payload ?? [];
    }

    // Option chain for an underlying on a specific expiry.
    // exchange = NSE/BSE, underlying = NIFTY/BANKNIFTY, expiryDate = YYYY-MM-DD.
    public async Task<OptionChainPayload?> GetOptionChainAsync(
        string exchange,
        string underlying,
        string expiryDate)
    {
        var url = $"{string.Format(GrowwEndpoint.OptionChain, Uri.EscapeDataString(exchange))}" +
                  $"/underlying/{Uri.EscapeDataString(underlying)}" +
                  $"?expiry_date={Uri.EscapeDataString(expiryDate)}";

        var result = await _growwHttp.GetAsync<GrowwResponse<OptionChainPayload>>(url);
        return result.Data?.Payload;
    }

    // Greeks for a specific FNO contract.
    public async Task<GreeksPayload?> GetGreeksAsync(
        string exchange,
        string underlying,
        string tradingSymbol,
        string expiry)
    {
        var url = GrowwEndpoint.LiveGreeks +
                  $"/exchange/{Uri.EscapeDataString(exchange)}" +
                  $"/underlying/{Uri.EscapeDataString(underlying)}" +
                  $"/trading_symbol/{Uri.EscapeDataString(tradingSymbol)}" +
                  $"/expiry/{Uri.EscapeDataString(expiry)}";

        var result = await _growwHttp.GetAsync<GrowwResponse<GreeksPayload>>(url);
        return result.Data?.Payload;
    }
}
