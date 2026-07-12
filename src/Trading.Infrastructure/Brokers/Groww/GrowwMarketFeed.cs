using Microsoft.Extensions.Logging;
using System.Text.Json;
using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Infrastructure.Brokers.Groww;

// Polling-based market feed for Groww (no WebSocket in Groww Trade API v1).
// Polls /v1/live-data/ohlc every second for subscribed symbols and emits tick JSON
// in the same format as the previous AngelOne WebSocket feed so MarketFeedWorker is unchanged.
public class GrowwMarketFeed : IMarketDataFeed
{
    private readonly IGrowwLiveDataService _liveData;
    private readonly ILogger<GrowwMarketFeed> _logger;

    public event Func<string, Task>? TickReceived;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);

    public GrowwMarketFeed(
        IGrowwLiveDataService liveData,
        ILogger<GrowwMarketFeed> logger)
    {
        _liveData = liveData;
        _logger = logger;
    }

    public async Task StartAsync(IEnumerable<TradingInstrument> tradingInstruments,
        CancellationToken ct)
    {
        var symbolList = tradingInstruments.ToList();

        // Build "EXCHANGE:SYMBOL" keys for the OHLC batch endpoint.
        // For FNO index tracking use the CASH segment LTP equivalent.
        var exchangeSymbols = symbolList
     .Select(s => $"{s.Exchange}_{s.Symbol}")
     .ToList();

        _logger.LogInformation("GrowwMarketFeed — polling {Count} symbol(s)", symbolList.Count);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                foreach (var symbol in symbolList)
                {
                    var singleExchangeSymbol = new List<string>
            {
                $"{symbol.Exchange}_{symbol.Symbol}"
            };

                    var singleSymbolListsingleSymbolList = new List<(string exchange, string token)>{(symbol.Exchange, symbol.Symbol)};

                    await PollOnceAsync(
                        singleExchangeSymbol,
                        singleSymbolListsingleSymbolList,
                        ct);
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GrowwMarketFeed — poll error; retrying in 5 s");
                try { await Task.Delay(5_000, ct); } catch (OperationCanceledException) { break; }
                continue;
            }

            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("GrowwMarketFeed — stopped");
    }

    public Task StopAsync() => Task.CompletedTask;

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task PollOnceAsync(
    List<string> exchangeSymbols,
    List<(string exchange, string token)> symbolList,
    CancellationToken ct)
    {
        var ohlcData = await _liveData.GetOhlcAsync(
            GrowwSegment.Cash,
            exchangeSymbols);

        var ltpData = await _liveData.GetLtpAsync(
            GrowwSegment.Cash,
            exchangeSymbols);

        if (ohlcData.Count == 0)
            ohlcData = await _liveData.GetOhlcAsync(
                GrowwSegment.Cash,
                exchangeSymbols);

        foreach (var (exchange, token) in symbolList)
        {
            var key = $"{exchange}_{token}";

            if (!ohlcData.TryGetValue(key, out var entry))
                continue;

            decimal ltp = 0;

            if (ltpData.TryGetValue(key, out var liveLtp))
                ltp =liveLtp!;

            // fallback only if LTP API fails
            if (ltp <= 0)
                ltp = entry.Close ?? 0m;

            var tickJson = JsonSerializer.Serialize(new
            {
                token = token,
                ltp = ltp,
                open = entry.Open,
                high = entry.High,
                low = entry.Low,
                close = entry.Close,
                volume = entry.Volume,
                exchangeTime = IstClock.Now
            });

            await InvokeHandlersAsync(tickJson);
        }
    }
    private async Task InvokeHandlersAsync(string tickJson)
    {
        var handlers = TickReceived?.GetInvocationList();
        if (handlers is null) return;

        foreach (var handler in handlers)
        {
            try { await ((Func<string, Task>)handler)(tickJson); }
            catch (Exception ex) { _logger.LogError(ex, "GrowwMarketFeed — tick handler threw"); }
        }
    }
}
