using Trading.Domain;
using Trading.Domain.Entities;
using Trading.SignalEngine.Services;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Fix 2 (guard half) - never take a trade whose index stop sits inside normal 5-minute noise.
///
/// Problem: the index stop is 1.5 x ATR(14) on 1-MINUTE candles, floored at 20 points. When 5-minute candles swing
/// 30-40 points, a 20-point stop is hit by ordinary noise even when the direction is right.
///
/// The other half of fix 2 (EnableAtr5mStop, in StrategyManager) sizes the stop from 5-minute ATR. This gate is the
/// backstop: it blocks when |EntryPrice - StopLoss| (index points) is below
/// <c>StopNoiseMinRatio</c> x the average High-Low range of the last <c>StopNoiseBars</c> closed 5m bars.
///
/// Post-signal gate. Off by default.
///   EnableStopNoiseGate  0/1      (default 0)
///   StopNoiseMinRatio    decimal  (default 1.0)
///   StopNoiseBars        int      (default 6)
/// Too few bars FAILS CLOSED (blocks). HeroZero is exempt (premium-percentage exits, not index stops).
/// </summary>
public class StopNoiseGate : IPostSignalGate
{
    internal const string EnableKey = "EnableStopNoiseGate";
    internal const string RatioKey = "StopNoiseMinRatio";
    internal const string BarsKey = "StopNoiseBars";
    internal const decimal DefaultRatio = 1.0m;
    internal const int DefaultBars = 6;

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        var ratio = ctx.StrategySettings.GetDecimal(RatioKey, ctx.Symbol, DefaultRatio);
        var bars = Math.Max(1, ctx.StrategySettings.GetInt(BarsKey, ctx.Symbol, DefaultBars));
        var stopPoints = Math.Abs(signal.EntryPrice - signal.StopLoss);

        return Task.FromResult(Evaluate(market.Candles5m, stopPoints, bars, ratio, ctx.Symbol));
    }

    /// <summary>Pure decision, exposed for testing. Null = proceed, otherwise the block reason.</summary>
    internal static string? Evaluate(IReadOnlyList<Candle> candles5m, decimal stopPoints, int bars, decimal ratio, string symbol)
    {
        var avgRange = FiveMinuteBars.AverageRange(candles5m, bars);

        if (avgRange is not { } noise)
            return $"Stop noise gate: fewer than {bars} closed 5m bars ({symbol})";

        var required = noise * ratio;
        return stopPoints < required
            ? $"Stop noise gate: stop {stopPoints:F1} pts < {ratio:F2} x avg 5m range {noise:F1} pts - inside noise ({symbol})"
            : null;
    }
}
