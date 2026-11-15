using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Trade only with the VWAP: a CALL needs LTP above VWAP, a PUT needs LTP below VWAP.
///
/// VWAP is the near-month index future's session VWAP shifted by the basis into index points (FuturesFlow), so
/// "index LTP above VWAP" means the future is above its own VWAP. Groww publishes no index candle volume, so this is the
/// only usable VWAP; it is populated from 2026-10-09 (SENSEX 2026-10-09 is invalid, see FuturesFlow).
///
/// Evidence (grid backtest, 2026-10-13, base rate over trend minutes, before costs, only ~3 sessions with VWAP): CALL with
/// LTP above VWAP PF 2.06 live-stop vs 1.45 base; PUT with LTP below VWAP PF 1.90 vs 1.90 base (no uplift, but the wrong
/// side was weak). Directional only - well under the 10-day bar; enable per symbol and watch it.
///
/// Post-signal gate: the direction only exists once the signal is built. Off by default (EnableVwapSideGate).
///   EnableVwapSideGate  0/1                                          (default off)
///   VwapSideGateSide    0 = CALL and PUT, 1 = CALL only, 2 = PUT only   (default 0)
/// Fail-open: when VWAP or LTP is unavailable the signal is let through (a missing feed is not a failed condition).
/// Equality blocks (CALL needs strictly above, PUT strictly below). HeroZero is exempt, like the other entry gates.
/// </summary>
public class VwapSideGate : IPostSignalGate
{
    internal const string EnableKey = "EnableVwapSideGate";
    internal const string SideKey = "VwapSideGateSide";
    internal const int SideBoth = 0, SideCallOnly = 1, SidePutOnly = 2;

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        var side = ctx.StrategySettings.GetInt(SideKey, ctx.Symbol, SideBoth);
        var reason = Evaluate(signal.SignalType, market.Ltp, market.Vwap, side);

        return Task.FromResult(reason is null ? null : $"{reason} ({ctx.Symbol})");
    }

    /// <summary>Whether the gate applies to this direction under the configured side.</summary>
    internal static bool AppliesTo(int side, string signalType) => side switch
    {
        SideCallOnly => signalType == TradingConst.SignalType.CALL,
        SidePutOnly => signalType == TradingConst.SignalType.PUT,
        _ => true,
    };

    /// <summary>The pure decision behind <see cref="BlockAsync"/>. Exposed for testing.</summary>
    internal static string? Evaluate(string signalType, decimal ltp, decimal? vwap, int side)
    {
        if (!AppliesTo(side, signalType))
            return null;

        if (vwap is null or <= 0 || ltp <= 0)
            return null;

        var isCall = signalType == TradingConst.SignalType.CALL;
        var withVwap = isCall ? ltp > vwap.Value : ltp < vwap.Value;

        return withVwap
            ? null
            : $"VWAP gate: {signalType} needs LTP {(isCall ? ">" : "<")} VWAP (LTP {ltp:F2}, VWAP {vwap.Value:F2})";
    }
}
