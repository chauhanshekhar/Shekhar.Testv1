using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// No new entries until the 1-minute indicators reflect today's session.
///
/// Why: EMA20/EMA50 (and EMA9/EMA21) are computed on the last 200 closed 1-minute candles, which at the open are mostly
/// YESTERDAY's. After a gap they lag for 30-60 minutes, so the trend read in the first part of the session is stale, and
/// the 1m ATR falls back to a window that straddles the overnight gap until 14 bars of today exist.
///
/// Separate from NoTradeWindowGate on purpose, so an existing midday window keeps working unchanged.
///
/// Post-signal gate. Off by default.
///   EnableOpeningWarmupGate   0/1                                          (default 0)
///   OpeningWarmupEndMinute    minutes after IST midnight, exclusive         (default 585 = 09:45)
/// Entries before 09:15 are never blocked here (the signal window handles that). HeroZero is exempt.
/// </summary>
public class OpeningWarmupGate : IPostSignalGate
{
    internal const string EnableKey = "EnableOpeningWarmupGate";
    internal const string EndKey = "OpeningWarmupEndMinute";
    internal const int MarketOpenMinute = 9 * 60 + 15;        // 09:15
    internal const int DefaultEndMinute = 9 * 60 + 45;        // 09:45

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        var end = ctx.StrategySettings.GetInt(EndKey, ctx.Symbol, DefaultEndMinute);
        var reason = Evaluate((int)IstClock.Now.TimeOfDay.TotalMinutes, end);

        return Task.FromResult(reason is null ? null : $"{reason} {signal.SignalType} ({ctx.Symbol})");
    }

    /// <summary>Pure time decision, exposed for testing. Null = proceed.</summary>
    internal static string? Evaluate(int minuteOfDay, int endMinute)
    {
        if (endMinute <= MarketOpenMinute)
            return null;

        return minuteOfDay >= MarketOpenMinute && minuteOfDay < endMinute
            ? $"Opening warm-up: no entries before {endMinute / 60:00}:{endMinute % 60:00} IST (now {minuteOfDay / 60:00}:{minuteOfDay % 60:00})"
            : null;
    }
}
