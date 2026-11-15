using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// "S1" PUT entry profile (proposed 2026-10-08): a PUT is allowed only when ALL of these hold at entry --
///   PCR within [PutS1PcrMin, PutS1PcrMax]            (default 1.00 - 1.20)
///   5m RSI within [PutS1RsiMin, PutS1RsiMax]         (default 30 - 33)
///   index ATR (points) within [PutS1AtrMin, Max]     (default 30 - 50)
///   option delta within [PutS1DeltaMin, Max]         (default -0.56 - -0.45)
///   index below the 5m EMA20, and 1m EMA20 below EMA50 (bearish)
/// The pasted rule also said "ExitReason != OPTION_SL_HIT"; that is only known after the exit, so it is NOT an entry condition and is not implemented.
///
/// Evidence is thin: on BANKNIFTY + SENSEX PUT since 2026-09-30 13:30 the profile matched 5 trades on 2 days (4 won, 1 lost, +10,865)
/// against 60 PUTs overall (net +15,497). It will block most PUT entries, which is the system's best side. Opt-in per symbol:
/// EnablePutS1Gate has no global row, only the symbol rows that docs/set-put-s1-gate.sql inserts. Off by default.
/// Missing data (5m RSI, option leg, 5m EMA) blocks the entry, since the profile cannot be confirmed. HeroZero and CALL are exempt.
/// </summary>
public class PutS1Gate : IPostSignalGate
{
    internal const string EnableKey = "EnablePutS1Gate";

    internal readonly record struct Limits(
        decimal PcrMin, decimal PcrMax, decimal RsiMin, decimal RsiMax,
        decimal AtrMin, decimal AtrMax, decimal DeltaMin, decimal DeltaMax);

    internal static readonly Limits Defaults = new(1.00m, 1.20m, 30m, 33m, 30m, 50m, -0.56m, -0.45m);

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (signal.SignalType != TradingConst.SignalType.PUT
            || signal.StrategyType == TradingConst.StrategyType.HeroZero
            || !ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        var s = ctx.StrategySettings;
        var sym = ctx.Symbol;
        var limits = new Limits(
            s.GetDecimal("PutS1PcrMin", sym, Defaults.PcrMin), s.GetDecimal("PutS1PcrMax", sym, Defaults.PcrMax),
            s.GetDecimal("PutS1RsiMin", sym, Defaults.RsiMin), s.GetDecimal("PutS1RsiMax", sym, Defaults.RsiMax),
            s.GetDecimal("PutS1AtrMin", sym, Defaults.AtrMin), s.GetDecimal("PutS1AtrMax", sym, Defaults.AtrMax),
            s.GetDecimal("PutS1DeltaMin", sym, Defaults.DeltaMin), s.GetDecimal("PutS1DeltaMax", sym, Defaults.DeltaMax));

        var (_, rsi5m) = SignalOrderPlacementService.EntryRsi(market);
        var delta = SignalAuditService.FindEntryLeg(market.OptionChain, signal)?.Delta;

        var reason = Evaluate(limits, rsi5m, market.PcrValue, market.AtrRaw, delta, market.Ltp, market.Ema20_5m, market.Ema20, market.Ema50);
        return Task.FromResult(reason is null ? null : $"{reason} ({sym})");
    }

    /// <summary>The pure decision behind <see cref="BlockAsync"/>. Exposed for testing. Returns the first failed condition.</summary>
    internal static string? Evaluate(
        Limits l, decimal? rsi5m, decimal pcr, decimal atrRaw, decimal? delta,
        decimal indexLtp, decimal ema20_5m, decimal ema20, decimal ema50)
    {
        if (rsi5m is null || delta is null || ema20_5m <= 0 || ema20 <= 0 || ema50 <= 0)
            return "PUT S1 gate: entry data unavailable (5m RSI / option delta / EMAs) - profile cannot be confirmed";

        if (pcr < l.PcrMin || pcr > l.PcrMax)
            return $"PUT S1 gate: PCR {pcr:F2} outside {l.PcrMin:F2}-{l.PcrMax:F2}";
        if (rsi5m < l.RsiMin || rsi5m > l.RsiMax)
            return $"PUT S1 gate: 5m RSI {rsi5m:F2} outside {l.RsiMin:F1}-{l.RsiMax:F1}";
        if (atrRaw < l.AtrMin || atrRaw > l.AtrMax)
            return $"PUT S1 gate: ATR {atrRaw:F1} pts outside {l.AtrMin:F0}-{l.AtrMax:F0}";
        if (delta < l.DeltaMin || delta > l.DeltaMax)
            return $"PUT S1 gate: delta {delta:F2} outside {l.DeltaMin:F2} to {l.DeltaMax:F2}";
        if (indexLtp >= ema20_5m)
            return $"PUT S1 gate: index {indexLtp:F2} not below 5m EMA20 {ema20_5m:F2}";
        if (ema20 >= ema50)
            return $"PUT S1 gate: 1m trend not bearish (EMA20 {ema20:F2} >= EMA50 {ema50:F2})";

        return null;
    }
}
