using Trading.Infrastructure;

namespace Trading.SignalEngine.Services;

public record AtrIndexRisk(decimal StopLoss, decimal Target1, decimal Target2);

/// <summary>
/// Single, reusable ATR-based index stop/target calculation — the ONLY place that computes
/// index-level StopLoss/Target1/Target2. Every strategy that needs them calls in here; nothing
/// duplicates this math. SignalEngineService and HeroZeroSignalEngine are
/// callers; ExitWorker is not — it only ever reads the Signal.StopLoss/Target1/Target2 saved at
/// signal-creation time and never recalculates them.
/// </summary>
public class AtrRiskService
{
    /// <summary>
    /// Index-level StopLoss/Target1/Target2 for one entry.
    /// Stop distance = ATR × AtrStopLossMultiplier, clamped to
    /// [MinimumIndexStopPoints, MaximumIndexStopPoints]. Targets are fixed multiples of that
    /// clamped stop distance, so reward/risk is constant across volatility regimes.
    /// CALL: SL below entry, targets above. PUT: SL above entry, targets below.
    /// symbol is accepted for forward-compatibility (e.g. a future per-instrument multiplier) —
    /// today's multipliers/clamp are global (TradingSettings), so it isn't used in the formula yet.
    /// </summary>
    public AtrIndexRisk CalculateIndexRisk(
        string symbol,
        decimal entryPrice,
        decimal atrRaw,
        bool isCall,
        TradingSettings settings)
    {
        // Targets are derived from the CLAMPED stop distance, not from raw ATR, so the configured
        // ratios hold in every volatility regime. AtrTargetNMultiplier is divided by
        // AtrStopLossMultiplier to strip the stop's own multiple back out, leaving a pure
        // multiple-of-risk: T1 = 1.5/1.5 = 1.0x, T2 = 1.95/1.5 = 1.3x.
        //
        // Previously these used raw atrRaw while the stop was clamped, so whenever the
        // MinimumIndexStopPoints floor bound (AtrRaw < 13.33 - 99.1% of observed NIFTY
        // snapshots) the stop widened and the targets did not. That put index T1 BELOW 1:1:
        // at AtrRaw 9.98 the trade risked 20.00 points to make 19.96.
        //
        // This matters beyond signal generation: ExitDecisionService exits live positions on
        // Signal.Target1 and Signal.StopLoss, so the distortion was reaching real exits.
        var stopDistance = GetStopDistance(atrRaw, settings);

        var stopMultiplier = settings.AtrStopLossMultiplier > 0
            ? settings.AtrStopLossMultiplier
            : 1.5m;

        var t1Distance = stopDistance * (settings.AtrTarget1Multiplier / stopMultiplier);
        var t2Distance = stopDistance * (settings.AtrTarget2Multiplier / stopMultiplier);

        return isCall
            ? new AtrIndexRisk(entryPrice - stopDistance, entryPrice + t1Distance, entryPrice + t2Distance)
            : new AtrIndexRisk(entryPrice + stopDistance, entryPrice - t1Distance, entryPrice - t2Distance);
    }

    private static decimal GetStopDistance(decimal atrRaw, TradingSettings settings)
    {
        var distance = atrRaw * settings.AtrStopLossMultiplier;
        return Math.Clamp(distance, settings.MinimumIndexStopPoints, settings.MaximumIndexStopPoints);
    }
}
