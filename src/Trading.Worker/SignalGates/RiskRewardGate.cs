using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

public class RiskRewardGate : IPostSignalGate
{
    /// <summary>
    /// Required reward-to-risk ratio, and it must stay in step with the configured target
    /// geometry: AtrTarget2Multiplier / AtrStopLossMultiplier = 1.95 / 1.5 = 1.3. Raising this
    /// above that ratio makes the gate unsatisfiable and suppresses every signal - which is
    /// exactly how the old 2.0 threshold silently blocked 99.1% of NIFTY snapshots.
    ///
    /// Trade-off, stated deliberately: 1.3:1 needs a 43.5% win rate to break even before costs,
    /// against 33.3% at 2.0:1. Chosen by the user; not supported by backtest evidence (8 days,
    /// PUT t=0.33 on non-overlapping windows).
    /// </summary>
    private const decimal MinRatio = 1.3m;

    /// <summary>
    /// Targets are produced at exactly MinRatio and then rounded to 2 decimals by SignalBuilder,
    /// so an exact-2.0 signal can land a paisa under the line and be rejected on rounding alone.
    /// This absorbs that rounding only — it is not a relaxation of the ratio.
    /// </summary>
    private const decimal RoundingTolerance = 0.02m;

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (signal.OptionSL <= 0 || signal.OptionT2 <= 0)
            return Task.FromResult<string?>(null);

        // Use entry trigger value so risk/reward are on the same scale as OptionSL/T2.
        var entry = PositionTrigger.EntryValue(signal);
        if (entry <= 0)
            return Task.FromResult<string?>(null);

        var risk   = entry - signal.OptionSL;
        var reward = signal.OptionT2 - entry;

        return Task.FromResult(risk > 0 && reward < (risk * MinRatio) - RoundingTolerance
            ? (string?)$"R:R {reward:F1}:{risk:F1} below {MinRatio:F1}:1 — signal suppressed ({ctx.Symbol})"
            : null);
    }
}
