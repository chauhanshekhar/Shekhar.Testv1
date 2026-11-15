using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;

namespace Trading.Worker.Exits;

/// <summary>
/// MFE/MAE excursion tracking and the ATR-based trailing stop. Extracted from ExitWorker
/// unchanged.
///
/// Both methods return whether a field actually changed rather than saving themselves — the
/// caller batches one save for the pair, and that batching is deliberate: the save must land
/// BEFORE the exit decision runs, because that call can throw and this scope's DbContext is
/// discarded on the next cycle.
/// </summary>
public class TrailingStopService
{
    // Ignore trailing-SL moves smaller than this — avoids a DB write every ~1s cycle for
    // sub-rupee noise (55.1 → 55.2 → 55.3 ...); only commits once the move is meaningful.
    private const decimal MinTrailingSlMovement = 0.5m;

    private readonly SystemSettingsService _settingsService;
    private readonly IInstrumentCache _instruments;

    public TrailingStopService(SystemSettingsService settingsService, IInstrumentCache instruments)
    {
        _settingsService = settingsService;
        _instruments = instruments;
    }

    // ── MFE / MAE update ─────────────────────────────────────────────────────
    // Returns whether a field changed — caller batches the save with other updates.
    public static bool UpdateExcursion(Signal signal, decimal optionLtp)
    {
        if (optionLtp <= 0) return false;
        var changed = false;
        if (optionLtp > signal.MfePrice) { signal.MfePrice = optionLtp; changed = true; }
        if (signal.MaePrice == 0 || optionLtp < signal.MaePrice) { signal.MaePrice = optionLtp; changed = true; }
        return changed;
    }

    // ── Trailing SL ──────────────────────────────────────────────────────────
    // Returns whether a field changed — caller batches the save with other updates.
    public async Task<bool> UpdateAsync(
        Signal signal,
        decimal triggerLtp,
        decimal entryTriggerValue,
        decimal underlyingLtp,
        decimal optionLtp,
        decimal currentDelta,
        Dictionary<string, decimal> latestAtrBySymbol,
        bool mfeIncreased,
        IRepository<TrailingStopLog> trailingLogRepo)
    {
        var cfg     = await _settingsService.GetAsync();
        var updated = false;

        // Only re-evaluate the ATR trailing distance on a fresh option-price high — if MFE
        // didn't move this cycle, the MFE-anchored target can't have moved up either, so
        // there's nothing to tighten. Cuts most cycles' work (and the resulting DB writes)
        // entirely. Also skipped below this signal's strategy-specific MinPremiumForAtrTrailing
        // (ATR distance stops being meaningful at deep-OTM/near-expiry premium scales) — HeroZero
        // and Expiry trade very different premium bands, so the threshold is per-strategy, not
        // global. The index-level fallback below is unaffected either way — it runs independently.
        var minPremiumForTrailing = cfg.MinPremiumForAtrTrailing.GetValueOrDefault(signal.SelectedStrategy, 5m);

        if (triggerLtp > 0 && entryTriggerValue > 0
            && mfeIncreased
            && optionLtp >= minPremiumForTrailing
            && triggerLtp > entryTriggerValue * (1m + cfg.TrailSLActivationPct / 100m))
        {
            // ATR-based trailing: MfePrice (highest option price seen) minus an ATR-derived
            // distance, replacing the old fixed-percentage-of-triggerLtp tiers. ATR is the
            // LATEST index ATR (read from SignalWorker's IndicatorSnapshot audit table — never
            // recalculated here), falling back to the frozen entry-time ATR when no snapshot is
            // fresh enough. Initial SL/Target1/Target2 still use entry-time ATR only (AtrRiskService,
            // unchanged) — this refresh applies to the trailing distance alone.
            var atr = latestAtrBySymbol.TryGetValue(signal.Symbol, out var latest) && latest > 0
                ? latest
                : signal.AtrRawAtEntry;

            // Delta is re-read live from the current option-chain snapshot each cycle, but
            // clamped to ±30% of entry delta — an unclamped swing (e.g. 0.55 → 0.15 as the
            // option runs deep ITM/OTM) would otherwise collapse optionAtrDistance and make
            // the trail far tighter than intended. Skipped if entryDelta is 0 (shouldn't
            // happen, but a 0-0 clamp would zero the distance outright rather than fall back).
            var entryDelta = Math.Abs(signal.DeltaAtEntry);
            var rawDelta = Math.Abs(currentDelta) > 0 ? Math.Abs(currentDelta) : entryDelta;
            var delta = entryDelta > 0
                ? Math.Clamp(rawDelta, entryDelta * 0.70m, entryDelta * 1.30m)
                : rawDelta;

            var optionAtrDistance = atr * delta * cfg.AtrStopLossMultiplier;

            if (optionAtrDistance > 0)
            {
                // Step-capped: move at most TrailingSLStepFraction × optionAtrDistance per
                // evaluation, so a single premium spike (MfePrice jump) can't yank the stop in
                // one shot — it approaches the ATR-derived target over several cycles instead.
                // Rounded once each, up front — not the combined result — so the stored value
                // never carries more precision than either input. targetSL (the destination,
                // not the live stop) is floored at entry premium, so trailing never AIMS below
                // breakeven — but signal.OptionSL itself still climbs toward it gradually via
                // the step cap below, by design, so the live stop can sit below breakeven for
                // several cycles right after activation if it started well below entry.
                var targetSL = Math.Max(signal.OptionLtp, signal.MfePrice - optionAtrDistance);
                targetSL = Math.Round(targetSL, 1);

                // Floored at MinTrailingSlMovement: a step cap smaller than the minimum
                // writable movement would deadlock the trailing stop forever (every cycle's
                // best-case gain sits below the write guard below, so it never fires again).
                // Math.Min against targetSL still prevents any overshoot past the real target.
                var maxStep = Math.Max(MinTrailingSlMovement, Math.Round(optionAtrDistance * cfg.TrailingSLStepFraction, 1));
                var newOptionSL = Math.Min(targetSL, signal.OptionSL + maxStep);

                if (newOptionSL - signal.OptionSL >= MinTrailingSlMovement)
                {
                    // Audit trail — one row per actual move, so "why did SL go from 74 to 79"
                    // is answerable later without ExitWorker keeping any history itself.
                    await trailingLogRepo.StageAsync(new TrailingStopLog
                    {
                        SignalId = signal.Id,
                        Symbol = signal.Symbol,
                        CreatedOn = IstClock.Now,
                        OldOptionSL = signal.OptionSL,
                        NewOptionSL = newOptionSL,
                        AtrRaw = atr,
                        Delta = delta,
                        OptionAtrDistance = optionAtrDistance,
                        TargetSL = targetSL,
                        MaxStep = maxStep,
                        MfePrice = signal.MfePrice,
                    });

                    signal.OptionSL = newOptionSL;
                    updated = true;
                }
            }
        }

        // Index-level trailing SL when option LTP unavailable
        if (optionLtp == 0)
        {
            var slPoints = _instruments.GetIndexStopPoints(signal.Symbol);
            if (signal.SignalType == TradingConst.SignalType.CALL && underlyingLtp > signal.EntryPrice)
            {
                var newSL = underlyingLtp - slPoints;
                if (newSL > signal.StopLoss) { signal.StopLoss = newSL; updated = true; }
            }
            else if (signal.SignalType == TradingConst.SignalType.PUT && underlyingLtp < signal.EntryPrice)
            {
                var newSL = underlyingLtp + slPoints;
                if (newSL < signal.StopLoss) { signal.StopLoss = newSL; updated = true; }
            }
        }

        return updated;
    }
}
