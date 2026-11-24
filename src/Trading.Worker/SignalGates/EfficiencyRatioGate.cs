using Trading.Domain;
using Trading.Domain.Entities;
using Trading.SignalEngine.Services;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Fix 3 - block entries in choppy markets, measured on 5-minute bars.
///
/// Problem: the trend check (EMA20 &gt; EMA50) runs on 1-minute candles with a 0.02% buffer, so it flips both ways in
/// chop and keeps passing. ChopGate exists but is off by default, is 1-minute based and only blocks the top decile.
///
/// Efficiency Ratio over the last <c>EfficiencyRatioBars</c> closed 5m bars = net move / total path. A market that
/// swings 30-40 points per bar but goes nowhere scores near 0. Below <c>MinEfficiencyRatio</c> the entry is blocked.
///
/// Post-signal gate. Off by default.
///   EnableEfficiencyRatioGate  0/1      (default 0)
///   MinEfficiencyRatio         decimal  (default 0.30)
///   EfficiencyRatioBars        int      (default 6 = last 30 minutes)
/// Too few bars or a flat path FAILS CLOSED (blocks). HeroZero is exempt.
/// </summary>
public class EfficiencyRatioGate : IPostSignalGate
{
    internal const string EnableKey = "EnableEfficiencyRatioGate";
    internal const string MinKey = "MinEfficiencyRatio";
    internal const string BarsKey = "EfficiencyRatioBars";
    internal const decimal DefaultMin = 0.30m;
    internal const int DefaultBars = 6;

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        var min = ctx.StrategySettings.GetDecimal(MinKey, ctx.Symbol, DefaultMin);
        var bars = Math.Max(2, ctx.StrategySettings.GetInt(BarsKey, ctx.Symbol, DefaultBars));

        return Task.FromResult(Evaluate(market.Candles5m, bars, min, ctx.Symbol));
    }

    /// <summary>Pure decision, exposed for testing. Null = proceed, otherwise the block reason.</summary>
    internal static string? Evaluate(IReadOnlyList<Candle> candles5m, int bars, decimal min, string symbol)
    {
        var er = FiveMinuteBars.EfficiencyRatio(candles5m, bars);

        if (er is not { } value)
            return $"Efficiency ratio gate: not enough 5m data or flat market ({bars} bars) ({symbol})";

        return value < min
            ? $"Efficiency ratio gate: 5m ER {value:F2} < {min:F2} over {bars} bars - choppy ({symbol})"
            : null;
    }
}
