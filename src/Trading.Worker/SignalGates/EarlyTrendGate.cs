using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Only enter in the direction of the 1-minute trend while the 5-minute trend has NOT yet turned the same way, i.e. buy the
/// turn, not the established move. A CALL is blocked when the live 5m EMA20 is already above the 5m EMA50; a PUT (if the side
/// setting includes it) is blocked when the 5m EMA20 is already below the 5m EMA50.
///
/// Evidence (docs/research/2026-10-08-early-call, backtest to 2026-10-07, before costs): CALL entries made before the 5m trend
/// turned up went wrong in 13.9-18.6% of cases vs 24.7-25.6% for late ones, hit the stop first 36-40% vs 50% of the time, and
/// had PF 1.51-2.04 vs 0.97-1.18 out of sample (and the same ordering in the August in-sample period). Real CALL trades since
/// 2026-10-01: 7 early trades +6,196, 21 late trades -2,512. Requiring the 5m trend to AGREE (higher-timeframe confirmation) is the
/// opposite of this and made results worse.
///
/// Post-signal gate: the direction only exists once the signal is built. Off by default (EnableEarlyTrendGate).
///   EnableEarlyTrendGate  0/1                                       (default off)
///   EarlyTrendGateSide    0 = CALL and PUT, 1 = CALL only, 2 = PUT only   (default 1: the PUT evidence is not consistent)
/// If either 5m EMA is not available yet (0) the gate lets the signal through. HeroZero is exempt, like the other entry gates.
///
/// EXPERIMENTAL gap-up morning exception (Version D1, docs/research/2026-11-04-gapup-morning-exception). HYPOTHESIS, NOT VALIDATED:
/// validation improved on 4-5 added trades, drawdown rose, and the idea was formed after inspecting the full sample. Off by default.
///   EnableGapUpMorningException  0/1   (default off)
/// When on, a CALL that the gate would block is allowed if ALL hold: session opened at least 0.30% above the
/// previous close, IST time before 10:00, and 5m EMA20 above EMA50. The 0.30% and 10:00 are fixed constants on purpose: they are the
/// only values tested, so no setting can quietly turn this into an untested strategy. HeroZero never uses it. Since 2026-11-05 it also applies in Live mode (user-approved, still unvalidated);
/// EnableGapUpMorningException=0 is the kill switch.
///
/// EXPERIMENTAL soft gate (docs/research/2026-11-11-early-trend-gapdays/ATR5M-DESIGN.md). HYPOTHESIS, NOT VALIDATED: PROMISING in the replay (17 added trades,
/// formed after inspecting the sample); for the forward PAPER trial only. Off by default.
///   EnableEarlyTrendSoftGate  0/1   (default off, per symbol)
/// When on, a CALL that the gate would block (and the gap-up morning exception did not exempt) is allowed if ALL hold: the session opened at least
/// 0.30% away from the previous close (gap up or down), IST time is 10:30 or later, 5m ATR(14) is available, and
/// (EMA20_5m - EMA50_5m) / ATR5m is below 0.30. Missing or stale ATR, gap or EMAs means NOT exempt: the existing block stands. The values are fixed constants
/// on purpose (the only ones tested). EnableEarlyTrendSoftGate=0 is the kill switch.
/// </summary>
public class EarlyTrendGate : IPostSignalGate
{
    internal const string EnableKey = "EnableEarlyTrendGate";
    internal const string SideKey = "EarlyTrendGateSide";
    internal const int SideBoth = 0, SideCallOnly = 1, SidePutOnly = 2;
    internal const string GapUpMorningKey = "EnableGapUpMorningException";
    internal const string SoftGateKey = "EnableEarlyTrendSoftGate";

    /// <summary>Fixed by design (the tested definition): allow while (EMA20_5m - EMA50_5m) / ATR5m is below this.</summary>
    internal const decimal SoftGateMaxNormalizedGap = 0.30m;

    /// <summary>Fixed by design: the session must have opened at least this many percent away from the previous close (either direction).</summary>
    internal const decimal SoftGateMinAbsGapPct = 0.30m;

    /// <summary>Fixed by design (the tested cutoff): the soft rule applies from 10:30 IST.</summary>
    internal static readonly TimeSpan SoftGateFrom = new(10, 30, 0);

    /// <summary>Fixed by design (the tested definition): session open at least this many percent above the previous close.</summary>
    internal const decimal GapUpMorningMinGapPct = 0.30m;

    /// <summary>Fixed by design (the tested cutoff): the exception ends at 10:00 IST.</summary>
    internal static readonly TimeSpan GapUpMorningCutoff = new(10, 0, 0);

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
        => BlockAt(ctx, market, signal, IstClock.Now, ct);

    /// <summary>The gate with the IST clock passed in, so the 10:00 / 10:30 boundaries can be tested without wall-clock timing.</summary>
    internal Task<string?> BlockAt(SignalContext ctx, MarketSnapshot market, Signal signal, DateTime now, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        var side = ctx.StrategySettings.GetInt(SideKey, ctx.Symbol, SideCallOnly);
        var reason = Evaluate(signal.SignalType, market.Ema20_5m, market.Ema50_5m, side);

        if (reason is not null)
        {
            var gapPct = GapPct(market.DayOpen, market.YesterdayClose);
            var switchOn = ctx.StrategySettings.GetBool(GapUpMorningKey, ctx.Symbol, false);

            if (GapUpMorningExempt(signal.SignalType, market.Ema20_5m, market.Ema50_5m, gapPct, now.TimeOfDay, switchOn))
            {
                // ELIGIBLE = the gate would have blocked this CALL and the exception let it through. Whether it then became a trade
                // depends on the gates after this one; those trades are found by matching Signals (docs/set-gapup-morning-exception.sql).
                ctx.ServiceProvider?.GetService<ILoggerFactory>()?.CreateLogger<EarlyTrendGate>().LogInformation(
                    "EarlyTrendGate GAPUP_MORNING_EXCEPTION_ELIGIBLE {Symbol} {SignalType} at {Time:HH:mm:ss} IST: gap={GapPct:F2}% " +
                    "5mEMA20={Ema20_5m:F2} 5mEMA50={Ema50_5m:F2} entry={Entry} (mode {Mode}, before {Cutoff:hh\\:mm})",
                    ctx.Symbol, signal.SignalType, now, gapPct, market.Ema20_5m, market.Ema50_5m, signal.EntryPrice,
                    ctx.Orders?.CurrentMode, GapUpMorningCutoff);
                return Task.FromResult<string?>(null);
            }

            var softOn = ctx.StrategySettings.GetBool(SoftGateKey, ctx.Symbol, false);
            if (softOn && signal.SignalType == TradingConst.SignalType.CALL)
            {
                var soft = SoftGateExempt(signal.SignalType, market.Ema20_5m, market.Ema50_5m, market.Atr5m, gapPct, now.TimeOfDay, softOn);
                // One line per evaluation so the paper trial can be compared with a backtest recompute of the same minute.
                ctx.ServiceProvider?.GetService<ILoggerFactory>()?.CreateLogger<EarlyTrendGate>().LogInformation(
                    "EarlyTrendGate SOFT_GATE_EVAL {Symbol} {SignalType} at {Time:HH:mm:ss} IST: gap={GapPct}% 5mEMA20={Ema20_5m:F2} 5mEMA50={Ema50_5m:F2} " +
                    "emaGap={EmaGap:F2} atr5m={Atr5m:F2} normalizedGap={NormalizedGap} threshold={Threshold} decision={Decision} (mode {Mode})",
                    ctx.Symbol, signal.SignalType, now, gapPct?.ToString("F2") ?? "n/a", market.Ema20_5m, market.Ema50_5m,
                    market.Ema20_5m - market.Ema50_5m, market.Atr5m, soft.NormalizedGap?.ToString("F3") ?? "n/a",
                    SoftGateMaxNormalizedGap, soft.Exempt ? "EXEMPT" : "BLOCK", ctx.Orders?.CurrentMode);
                if (soft.Exempt)
                    return Task.FromResult<string?>(null);
            }
        }

        return Task.FromResult(reason is null ? null : $"{reason} ({ctx.Symbol})");
    }

    /// <summary>
    /// Gap in percent of the session open over the previous close, or null when either price is missing, zero or negative.
    /// Computed here with its own fixed definition; it deliberately does not read OpeningRsiGapPct, which is a tunable RSI setting.
    /// </summary>
    internal static decimal? GapPct(decimal dayOpen, decimal yesterdayClose) =>
        dayOpen > 0 && yesterdayClose > 0 ? (dayOpen - yesterdayClose) / yesterdayClose * 100m : null;

    /// <summary>
    /// Pure decision for the experimental exception. Every input is an argument (including the time), so the boundary can be tested.
    /// True only for a CALL, with the switch on (any trading mode, Live included since 2026-11-05), a gap of at least 0.30%, a time strictly before 10:00 IST, and 5m EMA20
    /// above EMA50. A missing gap (null) or a missing 5m EMA (0) is never exempt.
    /// </summary>
    internal static bool GapUpMorningExempt(string signalType, decimal ema20_5m, decimal ema50_5m, decimal? gapPct,
        TimeSpan istTimeOfDay, bool switchOn) =>
        switchOn
        && signalType == TradingConst.SignalType.CALL
        && gapPct is { } gap && gap >= GapUpMorningMinGapPct
        && istTimeOfDay < GapUpMorningCutoff
        && ema20_5m > 0 && ema50_5m > 0 && ema20_5m > ema50_5m;

    internal readonly record struct SoftGateDecision(bool Exempt, decimal? NormalizedGap);

    /// <summary>
    /// Pure decision for the experimental soft gate. True only for a CALL, with the switch on, a session gap of at least 0.30% in either
    /// direction, a time of 10:30 IST or later, EMA20_5m above EMA50_5m (the case the gate blocks), a positive ATR5m, and
    /// (EMA20_5m - EMA50_5m) / ATR5m strictly below 0.30. Missing gap (null), EMA (0) or ATR (0) is never exempt. Mirrors the backtest condition
    /// EntryRules.EarlyTrendMaxGapAtr + EarlyTrendMaxGapAtrFromMinute + EarlyTrendMaxGapAtrGapDaysOnly.
    /// </summary>
    internal static SoftGateDecision SoftGateExempt(string signalType, decimal ema20_5m, decimal ema50_5m, decimal atr5m,
        decimal? gapPct, TimeSpan istTimeOfDay, bool switchOn)
    {
        decimal? normalized = ema20_5m > 0 && ema50_5m > 0 && atr5m > 0 ? (ema20_5m - ema50_5m) / atr5m : null;

        var exempt = switchOn
            && signalType == TradingConst.SignalType.CALL
            && gapPct is { } gap && Math.Abs(gap) >= SoftGateMinAbsGapPct
            && istTimeOfDay >= SoftGateFrom
            && ema20_5m > ema50_5m
            && normalized is { } n && n < SoftGateMaxNormalizedGap;

        return new SoftGateDecision(exempt, normalized);
    }

    /// <summary>Whether the gate applies to this direction under the configured side.</summary>
    internal static bool AppliesTo(int side, string signalType) => side switch
    {
        SideCallOnly => signalType == TradingConst.SignalType.CALL,
        SidePutOnly => signalType == TradingConst.SignalType.PUT,
        _ => true,
    };

    /// <summary>The pure decision behind <see cref="BlockAsync"/>. Exposed for testing.</summary>
    internal static string? Evaluate(string signalType, decimal ema20_5m, decimal ema50_5m, int side)
    {
        if (!AppliesTo(side, signalType))
            return null;

        if (ema20_5m <= 0 || ema50_5m <= 0)
            return null;

        var isCall = signalType == TradingConst.SignalType.CALL;
        var alreadyAligned = isCall ? ema20_5m > ema50_5m : ema20_5m < ema50_5m;

        return alreadyAligned
            ? $"Early-trend gate: 5m trend already {(isCall ? "up" : "down")} (EMA20 {ema20_5m:F2} vs EMA50 {ema50_5m:F2}) - " +
              $"{signalType} only enters before the 5m trend turns"
            : null;
    }
}
