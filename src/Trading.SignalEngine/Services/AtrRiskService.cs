using Trading.Infrastructure;

namespace Trading.SignalEngine.Services;

public record AtrIndexRisk(decimal StopLoss, decimal Target1, decimal Target2);

/// <summary>
/// Single, reusable ATR-based index stop/target calculation — the ONLY place that computes
/// index-level StopLoss/Target1/Target2. Every strategy that needs them calls in here; nothing
/// duplicates this math. SignalEngineService, ExpirySignalEngine and HeroZeroSignalEngine are
/// callers; ExitWorker is not — it only ever reads the Signal.StopLoss/Target1/Target2 saved at
/// signal-creation time and never recalculates them.
/// </summary>
public class AtrRiskService
{
    /// <summary>
    /// Index-level StopLoss/Target1/Target2 for one entry.
    /// Stop distance = ATR × AtrStopLossMultiplier, clamped to
    /// [MinimumIndexStopPoints, MaximumIndexStopPoints]. Targets are ATR × Target1/2Multiplier,
    /// unclamped. CALL: SL below entry, targets above. PUT: SL above entry, targets below.
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
        var stopDistance = GetStopDistance(atrRaw, settings);
        var t1Distance = atrRaw * settings.AtrTarget1Multiplier;
        var t2Distance = atrRaw * settings.AtrTarget2Multiplier;

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
