using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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

        var block = Evaluate(market.Candles5m, stopPoints, bars, ratio, ctx.Symbol);

        // One line per evaluation (pass or block) so a backtest/live disagreement can be traced to the bars live actually read.
        // Logging only: it never changes the decision and never throws.
        try
        {
            var noise = FiveMinuteBars.AverageRange(market.Candles5m, bars);
            var newest = market.Candles5m.Count > 0 ? market.Candles5m[^1] : null;
            ctx.ServiceProvider?.GetService<ILoggerFactory>()?.CreateLogger<StopNoiseGate>().LogInformation(
                "StopNoiseGate STOP_NOISE_EVAL {Symbol} {SignalType} at {Time:HH:mm:ss.fff} IST: stop={Stop} avgRange={AvgRange} required={Required} " +
                "newestBarOpenUtc={NewestBar:HH:mm} newestBarRange={NewestRange} bars={Bars} decision={Decision}",
                ctx.Symbol, signal.SignalType, IstClock.Now, stopPoints.ToString("F2"),
                noise?.ToString("F2") ?? "n/a", noise is { } n ? (n * ratio).ToString("F2") : "n/a",
                newest?.CandleTime, newest is null ? "n/a" : (newest.High - newest.Low).ToString("F2"),
                market.Candles5m.Count, block is null ? "PASS" : "BLOCK");
        }
        catch
        {
            // diagnostics only
        }

        return Task.FromResult(block);
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
