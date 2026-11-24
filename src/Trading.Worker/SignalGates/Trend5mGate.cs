using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Higher-timeframe trend filter: a CALL needs the 5-minute EMA20 above the 5-minute EMA50, a PUT needs it below.
///
/// Why: direction is otherwise decided only by 1-minute EMA20/EMA50, which flip both ways in chop. The old 5m trend
/// condition (EnableCall5mTrendFilter / EnablePut5mTrendFilter) lives only in CallEntryEvaluator / PutEntryEvaluator,
/// which nothing in the live pipeline instantiates - so those flags have NO live effect. This gate is the live version.
///
/// CAUTION from earlier research (see MarketDirectionGate): entries aligned with the 5m trend ("mature" entries) went
/// wrong more often than early ones. Paper-trade and compare blocked vs passed outcomes before relying on it.
///
/// Post-signal gate. Off by default.
///   EnableTrend5mGate          0/1   (default 0)
///   Trend5mGateSide            0 = CALL and PUT, 1 = CALL only, 2 = PUT only   (default 0)
///   Trend5mRequirePriceSide    0/1   also require LTP above (CALL) / below (PUT) the 5m EMA20   (default 0)
/// Missing 5m EMAs (0 - fewer than 50 closed 5m bars) FAILS CLOSED. HeroZero is exempt.
/// </summary>
public class Trend5mGate : IPostSignalGate
{
    internal const string EnableKey = "EnableTrend5mGate";
    internal const string SideKey = "Trend5mGateSide";
    internal const string PriceSideKey = "Trend5mRequirePriceSide";
    internal const int SideBoth = 0, SideCallOnly = 1, SidePutOnly = 2;

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        var side = ctx.StrategySettings.GetInt(SideKey, ctx.Symbol, SideBoth);
        var isCall = signal.SignalType == TradingConst.SignalType.CALL;
        if ((side == SideCallOnly && !isCall) || (side == SidePutOnly && isCall))
            return Task.FromResult<string?>(null);

        var requirePriceSide = ctx.StrategySettings.GetBool(PriceSideKey, ctx.Symbol, false);
        var reason = Evaluate(isCall, market.Ema20_5m, market.Ema50_5m, market.Ltp, requirePriceSide);

        return Task.FromResult(reason is null ? null : $"{reason} ({ctx.Symbol})");
    }

    /// <summary>Pure decision, exposed for testing. Null = proceed.</summary>
    internal static string? Evaluate(bool isCall, decimal ema20_5m, decimal ema50_5m, decimal ltp, bool requirePriceSide)
    {
        var side = isCall ? "CALL" : "PUT";

        if (ema20_5m <= 0 || ema50_5m <= 0)
            return $"5m trend gate: 5m EMA20/EMA50 unavailable - {side} blocked";

        var aligned = isCall ? ema20_5m > ema50_5m : ema20_5m < ema50_5m;
        if (!aligned)
            return $"5m trend gate: 5m EMA20 {ema20_5m:F2} not {(isCall ? "above" : "below")} EMA50 {ema50_5m:F2} - {side} blocked";

        if (requirePriceSide && ltp > 0 && (isCall ? ltp <= ema20_5m : ltp >= ema20_5m))
            return $"5m trend gate: LTP {ltp:F2} not {(isCall ? "above" : "below")} 5m EMA20 {ema20_5m:F2} - {side} blocked";

        return null;
    }
}
