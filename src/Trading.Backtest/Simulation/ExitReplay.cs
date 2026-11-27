using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Worker.Exits;

namespace Trading.Backtest.Simulation;

public sealed record ReplayResult(
    double PnlPoints, string ExitReason, int ExitStep, int Orders, int PartialSales,
    bool ReachedT1, bool ReachedMid, bool ReachedFinal, double MaxFavorable, double MaxAdverse);

/// <summary>
/// Replays one position through the live exit priority (ExitDecisionService conditions 1 to 8, same order) on a path of
/// premium moves observed at 1-minute closes. P&amp;L is in premium points for the whole position (each lot counts 1 unit);
/// multiply by the lot size for rupees. Fills at a stop are assumed at the stop level (the broker SL_M owns that exit), unless
/// stopFillAtClose is set (--stop-fill close): then min(stop, the minute close that crossed it).
///
/// What is mirrored, using the live static helpers where they exist so the numbers cannot drift:
///   ladder levels (<see cref="ExitDecisionService.LadderLevels"/>), step-lock (<see cref="ExitDecisionService.StepLockLevel"/>),
///   ladder routing (<see cref="ExitDecisionService.DecideLadder"/>), half-sale rule (<see cref="ExitDecisionService.ShouldSellHalfAtArm"/>).
/// What is NOT mirrored: DetermineAsync itself (async, bound to order/journal services), the SL_M fallback timer, tick-level
/// timing inside a minute, and the HeroZero strategy exits.
/// </summary>
public static class ExitReplay
{
    /// <param name="path">premium move after entry at each following minute close (entry = 0); never null</param>
    /// <param name="indexMovePerPremium">index points per premium point (1 / delta) - used for the index-T1 lock</param>
    public static ReplayResult Run(
        IReadOnlyList<double> path, double r, double atr, SymbolExitSettings cfg, ExitModel model, StopModel stopModel,
        bool isCall = true, bool stopFillAtClose = false, IReadOnlyList<double>? lowPath = null, IReadOnlyList<double>? highPath = null)
    {
        var live = stopModel == StopModel.Live;
        var lots = cfg.Lots ?? model.Lots;
        var entry = cfg.EntryPremium;
        var t1 = r * model.T1Mult / model.StopMult;
        var t2 = cfg.Target2R is { } t2R ? r * t2R : r * model.T2Mult / model.StopMult;   // Target2RMultiple row, else the ATR geometry
        var mid = cfg.MidR * r;
        var final = cfg.FinalR * r;
        var trailAct = entry * model.TrailPct / 100.0;
        var trailDist = atr * cfg.Delta * model.StopMult;
        var indexT1Premium = t1;                               // index T1 expressed in premium terms (index points x delta)

        double pnl = 0, optSl = -r, mfe = 0, mae = 0, last = 0;
        double? prot = null;
        int open = lots, stage = 0, orders = 1, partials = 0;
        bool indexT1Hit = false, reachedT1 = false, reachedMid = false;

        void Sell(int qty, double px)
        {
            qty = Math.Min(qty, open);
            pnl += qty * px;
            open -= qty;
            stage++;
            partials++;
            orders++;
            if (live) optSl = 0.0;                              // CompletePartialExitAsync: OptionSL = entry
        }

        for (var step = 0; step < path.Count; step++)
        {
            var pm = path[step];
            last = pm;
            // The peak drives the trailing stop and the step-lock level, and live sees every second, so with an estimated minute high
            // (--stop-touch low) the peak uses it. Targets and the close-based tests still use the close.
            var peak = highPath is not null && step < highPath.Count ? Math.Max(pm, highPath[step]) : pm;
            if (peak > mfe) mfe = peak;
            if (pm < mae) mae = pm;
            var p = entry + pm;

            // The stop as it stood when this minute OPENED. Used by the intra-minute touch test below, so a stop armed or raised by this
            // minute's own close can never be "hit" by the same minute's low (their order inside the minute is unknown).
            var stopAtOpen = live && prot is not null ? Math.Max(optSl, prot.Value) : optSl;

            // 1. HeroZero safety (premium collapsed below 2)
            if (p > 0 && p < 2)
                return Done(pnl + open * pm, TradingConst.ExitReason.HeroZero, step);

            // 2b. Research (--stop-touch low): the live ExitWorker checks the stop every second, so a minute whose ESTIMATED low (index bar
            // low x delta) reached the stop in force at its open was a stop-out even when the minute CLOSED above it. Filled at that stop.
            if (lowPath is not null && step < lowPath.Count && pm > stopAtOpen && lowPath[step] <= stopAtOpen)
            {
                var touchReason = live && prot is not null && prot.Value >= optSl
                    ? "PROTECTED_SL_HIT"
                    : TradingConst.ExitReason.ForOptionStop((decimal)(entry + optSl), (decimal)entry);
                return Done(pnl + open * stopAtOpen, touchReason, step);
            }

            // 3. index T1: no exit, locks a protected stop (never lowers one that is already armed)
            if (live && !indexT1Hit && pm >= indexT1Premium)
            {
                indexT1Hit = true;
                var lockProfit = pm;
                var indexLock = lockProfit > 5 ? 0.40 * lockProfit : -0.03 * entry;
                prot = Math.Max(prot ?? double.MinValue, indexLock);
            }

            // ATR trailing (ExitWorker), floored at entry, live only
            if (live && mfe >= trailAct && mfe - trailDist > optSl)
                optSl = Math.Max(0.0, mfe - trailDist);

            // 3b. breakeven arm: entry + buffer -> stop to entry; sell half at 2+ lots when enabled
            if (cfg.ArmOn && prot is null && pm >= cfg.Buffer)
            {
                prot = 0.0;
                if (cfg.PartialSaleAtArm && ExitDecisionService.ShouldSellHalfAtArm(open, SignalStatus.Active) && stage == 0)
                    Sell(CeilHalf(open), cfg.Buffer);
            }

            // 3c. step-lock
            if (live && cfg.StepLock && pm > 0)
            {
                decimal? ladderMid = cfg.Ladder ? (decimal)(entry + mid) : null;
                var lvl = (double)ExitDecisionService.StepLockLevel(
                    (decimal)entry, (decimal)cfg.Buffer, (decimal)(entry + t1), (decimal)(entry + t2), ladderMid, (decimal)(entry + mfe));
                if (lvl > 0 && lvl - entry > (prot ?? double.MinValue))
                    prot = lvl - entry;
            }

            // 4 + 5. protected stop / option stop (the higher level wins once armed)
            var stop = live && prot is not null ? Math.Max(optSl, prot.Value) : optSl;
            if (pm <= stop)
            {
                var reason = live && prot is not null && prot.Value >= optSl
                    ? "PROTECTED_SL_HIT"
                    : TradingConst.ExitReason.ForOptionStop((decimal)(entry + optSl), (decimal)entry);
                // Default: fill at the stop level (the broker SL_M owns the exit). stopFillAtClose: the check runs on a minute close, so a close
                // already below the stop is the better estimate of the live fill (real PROTECTED_SL_HIT exits filled below the stop on 34 of 36).
                var fill = stopFillAtClose ? Math.Min(stop, pm) : stop;
                return Done(pnl + open * fill, reason, step);
            }

            // 7L / 8L ladder, otherwise 7 (T2 full exit) and 8 (T1 partial)
            if (cfg.Ladder)
            {
                var action = ExitDecisionService.DecideLadder(
                    (decimal)p, (decimal)(entry + t1), (decimal)(entry + mid), (decimal)(entry + final), open, stage >= 1);
                switch (action)
                {
                    case ExitDecisionService.LadderAction.FinalExit:
                        reachedMid = true; reachedT1 = true;
                        return Done(pnl + open * final, TradingConst.ExitReason.OptionT5Hit, step);
                    case ExitDecisionService.LadderAction.T1FullExit:
                        reachedT1 = true;
                        return Done(pnl + t1, TradingConst.ExitReason.OptionT1Hit, step);
                    case ExitDecisionService.LadderAction.SellAtMid:
                        reachedMid = true; reachedT1 = true;
                        Sell(CeilHalf(open), mid);
                        break;
                    case ExitDecisionService.LadderAction.SellAtT1:
                        // a gap straight past the middle target books the T1 sale first; the mid sale follows next minute
                        reachedT1 = true;
                        Sell(CeilHalf(open), t1);
                        break;
                }
            }
            else
            {
                // 7T. EnableT2Partial: T1 half, T2 half of the rest, the last lot held for the stops (live routing helper).
                var t2Action = cfg.T2Partial
                    ? ExitDecisionService.DecideT2Partial((decimal)p, (decimal)(entry + t1), (decimal)(entry + t2), open, lots)
                    : ExitDecisionService.T2PartialAction.None;
                if (t2Action == ExitDecisionService.T2PartialAction.SellAtT1)
                {
                    reachedT1 = true;
                    if (stage == 0) Sell(CeilHalf(open), t1);
                    continue;
                }
                if (t2Action == ExitDecisionService.T2PartialAction.SellAtT2)
                {
                    reachedT1 = true;
                    Sell(CeilHalf(open), t2);
                    continue;
                }
                if (t2Action == ExitDecisionService.T2PartialAction.Hold)
                {
                    reachedT1 = true;
                    continue;
                }

                if (pm >= t2)
                {
                    reachedT1 = true;
                    return Done(pnl + open * t2, TradingConst.ExitReason.OptionT2Hit, step);
                }
                if (pm >= t1 && stage == 0)
                {
                    reachedT1 = true;
                    if (open == 1)
                        return Done(pnl + t1, TradingConst.ExitReason.OptionT1Hit, step);
                    Sell(CeilHalf(open), t1);
                }
            }

            if (open == 0)
                return Done(pnl, TradingConst.ExitReason.OptionT1Hit, step);
        }

        // 2. force exit at the end of the path (15:15 IST)
        return Done(pnl + open * last, TradingConst.ExitReason.ForceExitEod, Math.Max(0, path.Count - 1));

        ReplayResult Done(double total, string reason, int step) =>
            new(total, reason, step, orders + 1, partials, reachedT1, reachedMid, reason == TradingConst.ExitReason.OptionT5Hit, mfe, mae);
    }

    /// <summary>
    /// Research only (--fixed-bracket): a plain premium-point bracket instead of the live ladder. Stop at entry - points,
    /// target at entry + points, FULL exit at either, nothing else (no breakeven arm, step-lock, trailing, index-T1 lock or
    /// partial sales). Fills are assumed at the level, like the live replay's stop fills; the path is 1-minute closes, so an
    /// intra-minute touch that reverses by the close is not seen. P&amp;L is per lot counted as 1 unit, like <see cref="Run"/>.
    /// </summary>
    public static ReplayResult RunFixedBracket(IReadOnlyList<double> path, double points, int lots, double? targetPoints = null)
    {
        var target = targetPoints ?? points;   // --fixed-target: a different target distance than the stop
        double mfe = 0, mae = 0, last = 0;

        for (var step = 0; step < path.Count; step++)
        {
            var pm = path[step];
            last = pm;
            mfe = Math.Max(mfe, pm);
            mae = Math.Min(mae, pm);

            if (pm <= -points)
                return new(lots * -points, "FIXED_STOP_HIT", step, 2, 0, false, false, false, mfe, mae);
            if (pm >= target)
                return new(lots * target, "FIXED_TARGET_HIT", step, 2, 0, true, false, false, mfe, mae);
        }

        return new(lots * last, TradingConst.ExitReason.ForceExitEod, Math.Max(0, path.Count - 1), 2, 0, false, false, false, mfe, mae);
    }

    private static int CeilHalf(int open) => (open + 1) / 2;
}
