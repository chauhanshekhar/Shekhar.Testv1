using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Entry gate: skip an entry when the index has been moving back and forth, i.e. the sum of the last 15 closed
/// 1-minute ranges is more than 4.5 times the high-low span of those same 15 bars (chop = sum(High-Low) / (maxHigh - minLow)).
///
/// Evidence (docs/research/2026-11-12-sensex-noise-gate): 141 real SENSEX trades; chop above 4.5 is the top decile and caught 3 of
/// the 4 worst losses on 2026-11-12. HYPOTHESIS, NOT VALIDATED: the high-chop bucket flipped sign between halves (+0.7k / -11k), the
/// H1-fitted cutoff blocked only 3-6 second-half trades, and the cutoff was chosen after seeing the data. Forward trial only.
///
/// Post-signal gate. Off by default.
///   EnableChopGate  0/1   (default off; exact-symbol row, then global Symbol IS NULL row, so one global row enables every symbol
///                          and a per-symbol 0 row switches one symbol off). Evidence is SENSEX-only; NIFTY/BANKNIFTY are untested.
/// The gate does NOT look at the trading mode: it applies in whatever mode the DB sets (Paper or Live). EnableChopGate=0 is the kill
/// switch; set it before going Live unless you deliberately want it on. Threshold and window are fixed constants on purpose (the only
/// values tested). Fewer than 15 closed bars, or a zero span, lets the signal through. HeroZero is exempt.
/// </summary>
public class ChopGate : IPostSignalGate
{
    internal const string EnableKey = "EnableChopGate";
    internal const int Bars = 15;
    internal const decimal MaxChop = 4.5m;

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool(EnableKey, ctx.Symbol, false))
            return Task.FromResult<string?>(null);

        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        var chop = Chop(market.Candles1m);
        var blocked = chop is { } c && c > MaxChop;

        // One line per evaluation so the forward trial can be compared with the backtest recompute of the same minute.
        ctx.ServiceProvider?.GetService<ILoggerFactory>()?.CreateLogger<ChopGate>().LogInformation(
            "ChopGate CHOP_GATE_EVAL {Symbol} {SignalType} at {Time:HH:mm:ss} IST: chop={Chop} threshold={Threshold} decision={Decision} (mode {Mode})",
            ctx.Symbol, signal.SignalType, IstClock.Now, chop?.ToString("F2") ?? "n/a", MaxChop, blocked ? "BLOCK" : "PASS", ctx.Orders?.CurrentMode);

        return Task.FromResult(blocked
            ? $"Chop gate: 15-bar chop {chop:F2} > {MaxChop} ({ctx.Symbol})"
            : null);
    }

    /// <summary>
    /// Chop over the last 15 closed 1-minute candles (the list is chronological and closed-only), or null when there are fewer
    /// than 15 or the high-low span is not positive. Pure, exposed for testing.
    /// </summary>
    internal static decimal? Chop(IReadOnlyList<Candle> candles1m)
    {
        if (candles1m.Count < Bars) return null;

        decimal sum = 0m, hi = decimal.MinValue, lo = decimal.MaxValue;
        for (var i = candles1m.Count - Bars; i < candles1m.Count; i++)
        {
            var c = candles1m[i];
            sum += c.High - c.Low;
            if (c.High > hi) hi = c.High;
            if (c.Low < lo) lo = c.Low;
        }

        var span = hi - lo;
        return span > 0m ? sum / span : null;
    }
}
