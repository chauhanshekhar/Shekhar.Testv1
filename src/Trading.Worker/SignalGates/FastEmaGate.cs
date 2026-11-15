using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Block a CALL while the 1-minute EMA9 is not above the EMA21 (a PUT, if the side setting includes it, is blocked while EMA9 is
/// not below EMA21). By default it applies only on gap-up sessions, the only scope tested.
///
/// Evidence (docs/research/2026-11-04-fast-ema-9-21, costed): gap-up CALL 40 -> 37 trades, +19,323 -> +26,051, PF 2.14 -> 2.74, but
/// only 3 stop-outs were removed and the holdout slice was identical. Classified PROMISING, not validated; on gap-down sessions it
/// was worse. Shipped OFF so it can be enabled for paper trading and re-tested on the stored IndicatorSnapshot Ema9/Ema21 values.
///
/// Post-signal gate. Off by default (EnableFastEmaGate).
///   EnableFastEmaGate     0/1                                             (default off)
///   FastEmaGateSide       0 = CALL and PUT, 1 = CALL only, 2 = PUT only      (default 1)
///   FastEmaGateGapUpOnly  0/1: only when the session opened up by at least OpeningRsiGapPct (default 0.30%)   (default 1)
/// If either EMA is not available (0), or the gap cannot be classified while GapUpOnly is on, the gate lets the signal through.
/// HeroZero is exempt, like the other entry gates.
/// </summary>
public class FastEmaGate : IPostSignalGate
{
    internal const string EnableKey = "EnableFastEmaGate";
    internal const string SideKey = "FastEmaGateSide";
    internal const string GapUpOnlyKey = "FastEmaGateGapUpOnly";
    internal const string GapPctKey = "OpeningRsiGapPct";
    internal const int SideBoth = 0, SideCallOnly = 1, SidePutOnly = 2;

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        var side = ctx.StrategySettings.GetInt(SideKey, ctx.Symbol, SideCallOnly);
        if (ctx.StrategySettings.GetBool(GapUpOnlyKey, ctx.Symbol, true)
            && !OpenedGapUp(market.DayOpen, market.YesterdayClose, ctx.StrategySettings.GetDecimal(GapPctKey, ctx.Symbol, 0.30m)))
            return Task.FromResult<string?>(null);

        var reason = Evaluate(signal.SignalType, market.Ema9, market.Ema21, side);
        return Task.FromResult(reason is null ? null : $"{reason} ({ctx.Symbol})");
    }

    /// <summary>Whether the gate applies to this direction under the configured side.</summary>
    internal static bool AppliesTo(int side, string signalType) => side switch
    {
        SideCallOnly => signalType == TradingConst.SignalType.CALL,
        SidePutOnly => signalType == TradingConst.SignalType.PUT,
        _ => true,
    };

    /// <summary>Session open at least <paramref name="gapPct"/> percent above the previous close. False when either price is missing.</summary>
    internal static bool OpenedGapUp(decimal dayOpen, decimal yesterdayClose, decimal gapPct) =>
        dayOpen > 0 && yesterdayClose > 0 && (dayOpen - yesterdayClose) / yesterdayClose * 100m >= gapPct;

    /// <summary>The pure decision behind <see cref="BlockAsync"/>. Exposed for testing.</summary>
    internal static string? Evaluate(string signalType, decimal ema9, decimal ema21, int side)
    {
        if (!AppliesTo(side, signalType))
            return null;

        if (ema9 <= 0 || ema21 <= 0)
            return null;

        var isCall = signalType == TradingConst.SignalType.CALL;
        var aligned = isCall ? ema9 > ema21 : ema9 < ema21;

        return aligned
            ? null
            : $"Fast-EMA gate: 1m EMA9 {ema9:F2} vs EMA21 {ema21:F2} not {(isCall ? "above" : "below")} - {signalType} needs the fast EMA aligned";
    }
}
