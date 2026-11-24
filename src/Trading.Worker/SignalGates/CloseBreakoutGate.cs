using Trading.Domain;
using Trading.Domain.Entities;
using Trading.SignalEngine.Services;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Fix 1 - breakout must be confirmed by a CLOSED 5-minute candle, not a 1-minute poke.
///
/// Problem: entry condition 4 is "LTP &gt; Prev5mHigh", where LTP is the last closed 1-minute close and Prev5mHigh is
/// the high of ONE previous 5m bar. In a choppy market a single 1m close pokes above one 5m high, the entry fires at
/// the top of the swing, and the move reverses into the stop.
///
/// Rule (CALL; PUT is the mirror):
///   1. The last CLOSED 5m bar closed above the highest High of the <c>BreakoutLookbackCandles</c> 5m bars before it.
///   2. LTP is still above that level (the breakout has not already failed).
///
/// Post-signal gate. Off by default.
///   EnableCloseBreakoutGate  0/1  (default 0; exact symbol row, then global row)
///   BreakoutLookbackCandles  int  (default 5)
/// Too few 5m bars FAILS CLOSED (blocks) - an unconfirmed breakout must never read as confirmed. HeroZero is exempt
/// (its own entry rules).
/// </summary>
public class CloseBreakoutGate : IPostSignalGate
{
    internal const string EnableKey = "EnableCloseBreakoutGate";
    internal const string LookbackKey = "BreakoutLookbackCandles";
    internal const int DefaultLookback = 5;

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        var call = signal.SignalType == TradingConst.SignalType.CALL;
        var lookback = Math.Max(1, ctx.StrategySettings.GetInt(LookbackKey, ctx.Symbol, DefaultLookback));

        return Task.FromResult(Evaluate(market.Candles5m, market.Ltp, lookback, call, ctx.Symbol));
    }

    /// <summary>Pure decision, exposed for testing. Null = proceed, otherwise the block reason.</summary>
    internal static string? Evaluate(IReadOnlyList<Candle> candles5m, decimal ltp, int lookback, bool call, string symbol)
    {
        var side = call ? "CALL" : "PUT";
        var result = FiveMinuteBars.CloseBreakout(candles5m, lookback, call);

        if (result is not { } r)
            return $"Close breakout gate: fewer than {lookback + 1} closed 5m bars ({symbol} {side})";

        var lastClose = candles5m[^1].Close;

        if (!r.Broke)
            return call
                ? $"Close breakout gate: 5m close {lastClose:F2} not above {lookback}-bar high {r.Level:F2} ({symbol} {side})"
                : $"Close breakout gate: 5m close {lastClose:F2} not below {lookback}-bar low {r.Level:F2} ({symbol} {side})";

        var stillBeyond = call ? ltp > r.Level : ltp < r.Level;
        if (!stillBeyond)
            return $"Close breakout gate: LTP {ltp:F2} back inside breakout level {r.Level:F2} - breakout failed ({symbol} {side})";

        return null;
    }
}
