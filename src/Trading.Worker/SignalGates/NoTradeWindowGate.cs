using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Blocks new entries while the IST clock is inside a configured window, optionally for one direction only.
/// A post-signal gate because the direction (CALL / PUT) only exists once the signal is built.
/// Existing positions are never touched: exits are handled by ExitWorker, not by this gate.
///
/// Off by default (EnableNoTradeWindowGate). Per-symbol capable like every StrategySettings key.
///   EnableNoTradeWindowGate    0/1               (default off)
///   NoTradeWindowStartMinute   minutes after IST midnight, inclusive (default 660 = 11:00)
///   NoTradeWindowEndMinute     minutes after IST midnight, exclusive (default 750 = 12:30)
///   NoTradeWindowSide          0 = CALL and PUT, 1 = CALL only, 2 = PUT only   (default 0)
/// An end at or before the start disables the window rather than wrapping past midnight.
/// </summary>
public class NoTradeWindowGate : IPostSignalGate
{
    internal const string EnableKey = "EnableNoTradeWindowGate";
    internal const string StartKey = "NoTradeWindowStartMinute";
    internal const string EndKey = "NoTradeWindowEndMinute";
    internal const string SideKey = "NoTradeWindowSide";
    internal const int DefaultStartMinute = 11 * 60;        // 11:00
    internal const int DefaultEndMinute = 12 * 60 + 30;     // 12:30
    internal const int SideBoth = 0, SideCallOnly = 1, SidePutOnly = 2;

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        var side = ctx.StrategySettings.GetInt(SideKey, ctx.Symbol, SideBoth);
        if (!AppliesTo(side, signal.SignalType))
            return Task.FromResult<string?>(null);

        var start = ctx.StrategySettings.GetInt(StartKey, ctx.Symbol, DefaultStartMinute);
        var end = ctx.StrategySettings.GetInt(EndKey, ctx.Symbol, DefaultEndMinute);
        var reason = Evaluate((int)IstClock.Now.TimeOfDay.TotalMinutes, start, end);

        return Task.FromResult(reason is null ? null : $"{reason} {signal.SignalType} ({ctx.Symbol})");
    }

    /// <summary>Whether the window applies to this direction under the configured side.</summary>
    internal static bool AppliesTo(int side, string signalType) => side switch
    {
        SideCallOnly => signalType == TradingConst.SignalType.CALL,
        SidePutOnly => signalType == TradingConst.SignalType.PUT,
        _ => true,
    };

    /// <summary>The pure time decision behind <see cref="BlockAsync"/>. Exposed for testing.</summary>
    internal static string? Evaluate(int minuteOfDay, int startMinute, int endMinute)
    {
        if (endMinute <= startMinute)
            return null;

        return minuteOfDay >= startMinute && minuteOfDay < endMinute
            ? $"No-trade window {Format(startMinute)}-{Format(endMinute)} IST (now {Format(minuteOfDay)})"
            : null;
    }

    private static string Format(int minuteOfDay) => $"{minuteOfDay / 60:00}:{minuteOfDay % 60:00}";
}
