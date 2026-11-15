using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Blocks signals whose index stop is a fixed floor rather than a volatility measure.
///
/// AtrRiskService clamps the index stop up to MinimumIndexStopPoints. Below
/// AtrRaw = MinimumIndexStopPoints / AtrStopLossMultiplier (20 / 1.5 = 13.33) the clamp binds, so
/// the stop no longer scales with the market and the position is sized by a constant instead of
/// by ATR.
///
/// This used to be enforced by accident: the clamp widened risk while targets stayed on raw ATR,
/// so RiskRewardGate rejected the signal with a misleading "R:R below 2:1". Targets now derive
/// from the clamped stop, so that side effect is gone and the condition is stated directly.
///
/// This is a POST gate, not a pre gate, because the exemption below depends on the selected
/// strategy — which StrategyManager has not chosen yet when PreGates run.
///
/// The floor's multiplier is IndexStopFloorAtrMultiplier when set, else AtrStopLossMultiplier. They were
/// decoupled on 2026-10-07 so the stop can move to 1.25 while the floor stays at 20 / 1.5 = 13.33: the
/// coupled 16.00 floor blocked profitable ATR 13.3-16 entries (docs/research/2026-10-07-stop-width).
/// </summary>
public class IndexStopFloorGate : IPostSignalGate
{
    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool("EnableIndexStopFloor", ctx.Symbol, true))
            return Task.FromResult<string?>(null);

        // HeroZero is exempt. Its option exits are percentages of premium (50% SL, 2x/4x targets)
        // rather than ATR-derived levels, so a clamped index stop does not distort its risk the
        // way it distorts a directional signal. It was trading through this condition before the
        // condition was made explicit — every HERO_ZERO signal on record was generated below the
        // floor — so applying it here would silently retire the strategy rather than fix anything.
        if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            return Task.FromResult<string?>(null);

        var minAtrRaw = MinAtrRaw(ctx.Settings);

        return Task.FromResult(market.AtrRaw < minAtrRaw
            ? $"ATR {market.AtrRaw:F2} pts < index stop floor {minAtrRaw:F2} pts " +
              $"(stop would clamp to {ctx.Settings.MinimumIndexStopPoints:F0}) ({ctx.Symbol})"
            : null);
    }

    /// <summary>The AtrRaw below which a directional entry is blocked. Exposed for testing.</summary>
    internal static decimal MinAtrRaw(TradingSettings settings)
    {
        var floorMultiplier = settings.IndexStopFloorAtrMultiplier > 0
            ? settings.IndexStopFloorAtrMultiplier
            : settings.AtrStopLossMultiplier > 0
                ? settings.AtrStopLossMultiplier
                : 1.5m;

        return settings.MinimumIndexStopPoints / floorMultiplier;
    }
}
