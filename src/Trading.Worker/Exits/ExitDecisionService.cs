using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;

namespace Trading.Worker.Exits;

/// <summary>
/// The exit rule ladder for one open signal, in priority order. Extracted from ExitWorker
/// unchanged — the ORDER of these checks is the trading logic, so nothing here was reordered,
/// merged, or short-circuited in the move.
///
///   1. HeroZero safety      — premium collapsed below ₹2
///   2. Force exit EOD
///   3. Index Target1        — no exit; locks in ProtectedSL (mutates the signal)
///   3b. Partial T1 / breakeven arm — entry + BreakevenBufferPoints: sell half (2+ lots), stop -> entry
///   4. Protected SL         — only after Index T1
///   5. Option stop loss     — broker SL_M owns this for SINGLE/HERO_ZERO; the bounded MARKET
///                             fallback here covers a broker/API outage only
///   6. Index stop loss
///   7. Option Target2       — full exit
///   8. Option Target1       — partial booking when more than one lot is open
///   7L/8L. Target ladder    — replaces 7 and 8 when EnableTargetLadder is on: 50% at T1 (1R),
///                             50% of the rest at TargetLadderMidR (3R), the rest at
///                             TargetLadderFinalR (5R)
///
/// Returns (null, 0) for "hold" — including after a partial exit has been placed, since that
/// path is not a full close.
/// </summary>
public class ExitDecisionService
{
    private readonly ILogger<ExitDecisionService> _logger;
    private readonly SystemSettingsService _settingsService;
    private readonly IStrategySettingsService _strategySettings;
    private readonly AppErrorLogger _errorLogger;
    private readonly PositionCloseService _positionClose;

    public ExitDecisionService(
        ILogger<ExitDecisionService> logger,
        SystemSettingsService settingsService,
        IStrategySettingsService strategySettings,
        AppErrorLogger errorLogger,
        PositionCloseService positionClose)
    {
        _logger = logger;
        _settingsService = settingsService;
        _strategySettings = strategySettings;
        _errorLogger = errorLogger;
        _positionClose = positionClose;
    }

    /// <summary>Compiled fallback when no BreakevenBufferPoints row exists (premium points above entry).</summary>
    internal const decimal DefaultBreakevenBufferPoints = 15m;

    /// <summary>Half of one lot cannot be sold; a signal already awaiting a partial fill must not sell again.</summary>
    internal static bool ShouldSellHalfAtArm(int openLots, string status)
        => openLots > 1 && status == SignalStatus.Active;

    internal const decimal DefaultLadderMidR = 3m;
    internal const decimal DefaultLadderFinalR = 5m;

    /// <summary>
    /// Target-ladder premium levels. R is the ORIGINAL stop distance, recovered from OptionT1 (which
    /// SignalEngine places at AtrTarget1Multiplier / AtrStopLossMultiplier R and the fill re-anchor
    /// keeps proportional). OptionSL cannot be used: it moves to entry after the first partial.
    /// Null when the levels cannot be formed (no T1 above entry, bad multipliers, or R values out
    /// of order).
    /// </summary>
    internal static (decimal Mid, decimal Final)? LadderLevels(
        Signal signal, decimal target1Multiplier, decimal stopMultiplier, decimal midR, decimal finalR)
    {
        if (signal.OptionLtp <= 0 || signal.OptionT1 <= signal.OptionLtp
            || target1Multiplier <= 0 || stopMultiplier <= 0 || midR <= 0 || finalR <= midR)
            return null;

        var r = (signal.OptionT1 - signal.OptionLtp) * stopMultiplier / target1Multiplier;
        return (Math.Round(signal.OptionLtp + midR * r, 2), Math.Round(signal.OptionLtp + finalR * r, 2));
    }

    /// <summary>
    /// Step-lock level for a peak premium: each level reached locks the stop one level below.
    /// Returns 0 when no level has been reached. Levels that are not above entry are ignored.
    /// </summary>
    internal static decimal StepLockLevel(
        decimal entry, decimal buffer, decimal t1, decimal t2, decimal? ladderMid, decimal peak)
    {
        if (entry <= 0 || peak <= entry)
            return 0m;

        if (ladderMid is { } mid && mid > t2 && t2 > entry && peak >= mid)
            return Math.Round(t2, 2);
        if (t2 > t1 && t1 > entry && peak >= t2)
            return Math.Round(t1, 2);
        if (t1 > entry && buffer > 0 && peak >= t1)
            return Math.Round(entry + buffer, 2);
        if (buffer > 0 && peak >= entry + buffer)
            return Math.Round(entry, 2);
        return 0m;
    }

    /// <summary>
    /// True when a broker SL_M is resting at (or above) the protected level for a single-leg
    /// position: the broker owns that exit, and a MARKET sell here would be a second SELL.
    /// </summary>
    internal static bool BrokerStopCovers(Signal signal)
        => TradingConst.IsSingleLegStrategy(signal.StrategyType)
           && signal.StopLossStatus == "PLACED"
           && !string.IsNullOrEmpty(signal.StopLossOrderId)
           && signal.StopLossTriggerPrice >= signal.ProtectedSL - 0.05m;

    public enum T2PartialAction { None, SellAtT1, SellAtT2, Hold }

    /// <summary>
    /// Routing for EnableT2Partial (3+ lots only; 1-2 lot trades keep the legacy T1/T2 behaviour).
    /// 4 lots: T1 sells 2, T2 sells 1, the last lot is left to the stops / trail / 15:15 force exit.
    /// Price gapping past T2 before any partial books the T1 sale first; T2 follows next cycle.
    /// Hold = the last lot of a partially-sold trade: step 8 must NOT full-exit it at T1.
    /// </summary>
    internal static T2PartialAction DecideT2Partial(
        decimal triggerLtp, decimal optionT1, decimal optionT2, int openLots, int originalLots)
    {
        if (originalLots <= 2 || optionT2 <= 0 || openLots < 1) return T2PartialAction.None;

        var partialDone = openLots < originalLots;
        if (openLots == 1)
            return partialDone && optionT1 > 0 && triggerLtp >= optionT1
                ? T2PartialAction.Hold : T2PartialAction.None;

        return triggerLtp >= optionT2
            ? (partialDone ? T2PartialAction.SellAtT2 : T2PartialAction.SellAtT1)
            : T2PartialAction.None;
    }

    public enum LadderAction { Hold, FinalExit, SellAtMid, SellAtT1, T1FullExit }

    /// <summary>
    /// The pure routing of the target ladder (7L/8L). Stops, breakeven and force exit are decided
    /// earlier in the ladder and always win. Price can gap past T1 straight to the middle target:
    /// then the T1 sale is booked first and the middle-target sale follows on a later cycle.
    /// </summary>
    internal static LadderAction DecideLadder(
        decimal triggerLtp, decimal optionT1, decimal mid, decimal final, int openLots, bool partialDone)
    {
        if (triggerLtp >= final)
            return LadderAction.FinalExit;

        if (triggerLtp >= mid && openLots > 1)
            return partialDone ? LadderAction.SellAtMid : LadderAction.SellAtT1;

        if (optionT1 > 0 && triggerLtp >= optionT1)
        {
            if (partialDone) return LadderAction.Hold;          // runners go on to the next level
            return openLots > 1 ? LadderAction.SellAtT1 : LadderAction.T1FullExit;
        }

        return LadderAction.Hold;
    }

    public async Task<(string? exitReason, decimal exitPrice)> DetermineAsync(
        Signal signal,
        decimal triggerLtp,
        decimal optionLtp,
        decimal leg2CurrentLtp,
        decimal underlyingLtp,
        bool forceExit,
        DateTime nowIst,
        Dictionary<string, List<OptionChainData>> chainBySymbol,
        IOrderService orders,
        IRepository<TradeJournal> journalRepo,
        IRepository<Order> orderRepo,
        IUnitOfWork uow,
        TradeCompletionService tradeCompletion)
    {
        var exitPrice = optionLtp > 0
            ? optionLtp
            : underlyingLtp;


        // ================================
        // 1. HERO ZERO SAFETY
        // ================================

        if (optionLtp > 0 && optionLtp < 2)
        {
            return (
                TradingConst.ExitReason.HeroZero,
                exitPrice);
        }


        // ================================
        // 2. FORCE EXIT EOD
        // ================================

        if (forceExit)
        {
            return (
                TradingConst.ExitReason.ForceExitEod,
                exitPrice);
        }



        // ================================
        // 3. INDEX TARGET1 PROTECTION
        // (Do not exit, lock trade)
        // ================================
        if (!signal.IndexT1Hit
            && signal.Target1 > 0
            && underlyingLtp > 0
            && optionLtp > 0
            && signal.OptionLtp > 0)
        {
            var indexT1Reached =
                signal.SignalType == TradingConst.SignalType.CALL
                    ? underlyingLtp >= signal.Target1
                    : underlyingLtp <= signal.Target1;

            if (indexT1Reached)
            {
                signal.IndexT1Hit = true;

                // move protection near cost/profit
                var lockProfit = optionLtp - signal.OptionLtp;

                // Only ever RAISES the stop: a breakeven or step-lock already armed must not be
                // lowered by this lock (entry x 0.97 is below breakeven).
                var indexLock = lockProfit > 5
                    ? signal.OptionLtp + (lockProfit * 0.40m)
                    : signal.OptionLtp * 0.97m;
                signal.ProtectedSL = Math.Max(signal.ProtectedSL, indexLock);
            }
        }

        // ================================
        // 3b. PARTIAL T1 -> SELL HALF + ARM BREAKEVEN
        // ================================
        // Once the premium has travelled BreakevenBufferPoints (per symbol, StrategySettings) above
        // entry, e.g. entry 140 + 15 = 155: sell 50% of the open lots (profit booked) and move the
        // stop on the rest to entry (140). OptionSL and OptionT1/T2 are NOT changed; the remaining
        // lots still run to T1/T2.
        //
        // Reuses ProtectedSL (the only other writer is condition 3) so no schema change is needed;
        // ProtectedSL > 0 IS the "armed" flag, which is why condition 4 below does not require
        // IndexT1Hit — either path may arm it. Guarded by EnableBreakevenArm.
        //
        // Sell-half needs 2+ lots. At 1 lot only the breakeven stop is armed (half a lot cannot be
        // sold; earlier measurement showed selling half on Breakdown/Scalping cost more upside
        // than the round-trips it saved, so 1-lot arm-only is the safe degenerate case).
        //
        // EnableBreakevenPartialSale (per symbol, default on) turns the sale off while keeping the stop.
        //
        // The sale is attempted ONCE, at the moment of arming. It is not retried: a rejected
        // partial (e.g. broker margin on a closing sell) must not loop against the broker. The
        // breakeven stop protects the full remaining size regardless.
        var armEnabled = _strategySettings.GetBool("EnableBreakevenArm", signal.Symbol, true);

        if (armEnabled
            && signal.ProtectedSL <= 0
            && optionLtp > 0
            && signal.OptionLtp > 0)
        {
            var buffer = _strategySettings.GetDecimal(
                "BreakevenBufferPoints", signal.Symbol, DefaultBreakevenBufferPoints);
            var armLevel = signal.OptionLtp + buffer;

            if (buffer > 0 && optionLtp >= armLevel)
            {
                var openLotsAtArm = signal.RemainingQty > 0 ? signal.RemainingQty : signal.Quantity;
                // Per-symbol switch: off = arm the breakeven stop only, never sell at the arm level.
                var partialSaleEnabled = _strategySettings.GetBool(
                    "EnableBreakevenPartialSale", signal.Symbol, true);
                var sellHalf = partialSaleEnabled && ShouldSellHalfAtArm(openLotsAtArm, signal.Status);

                signal.ProtectedSL = Math.Round(signal.OptionLtp, 2);

                _logger.LogInformation(
                    "BREAKEVEN ARMED for signal {Id} {Symbol} — premium {Ltp:F2} reached entry {Entry:F2} + "
                    + "{Buffer} = {Arm:F2}; stop moved to entry. SellHalf={SellHalf} (open lots {Lots}). "
                    + "OptionSL/OptionT1 unchanged.",
                    signal.Id, signal.Symbol, optionLtp, signal.OptionLtp, buffer, armLevel,
                    sellHalf, openLotsAtArm);

                if (sellHalf)
                {
                    await _positionClose.HandlePartialExitAsync(
                        signal, optionLtp, nowIst, chainBySymbol,
                        orders, journalRepo, orderRepo, uow, tradeCompletion);

                    return (null, 0);
                }
            }
        }

        // ================================
        // 3c. STEP-LOCK (EnableStepLock, per symbol, off by default)
        // ================================
        // Each level the premium has reached (peak = max(current, MfePrice)) locks the stop one
        // level below it. ProtectedSL only ever rises; the broker SL_M follows it
        // (StopLossSyncService.BrokerStopTrigger).
        //   entry + BreakevenBufferPoints -> entry
        //   T1                            -> entry + BreakevenBufferPoints
        //   T2                            -> T1
        //   ladder middle target (3R)     -> T2      (only when EnableTargetLadder is on)
        if (optionLtp > 0 && signal.OptionLtp > 0
            && _strategySettings.GetBool("EnableStepLock", signal.Symbol, false))
        {
            var stepBuffer = _strategySettings.GetDecimal(
                "BreakevenBufferPoints", signal.Symbol, DefaultBreakevenBufferPoints);

            decimal? ladderMid = null;
            if (_strategySettings.GetBool("EnableTargetLadder", signal.Symbol, false))
            {
                var stepCfg = await _settingsService.GetAsync();
                ladderMid = LadderLevels(
                    signal, stepCfg.AtrTarget1Multiplier, stepCfg.AtrStopLossMultiplier,
                    _strategySettings.GetDecimal("TargetLadderMidR", signal.Symbol, DefaultLadderMidR),
                    _strategySettings.GetDecimal("TargetLadderFinalR", signal.Symbol, DefaultLadderFinalR))?.Mid;
            }

            var peak = Math.Max(optionLtp, signal.MfePrice);
            var stepLock = StepLockLevel(signal.OptionLtp, stepBuffer, signal.OptionT1, signal.OptionT2, ladderMid, peak);

            if (stepLock > signal.ProtectedSL)
            {
                _logger.LogInformation(
                    "STEP-LOCK for signal {Id} {Symbol} — peak premium {Peak:F2}; stop raised {Old:F2} -> {New:F2}",
                    signal.Id, signal.Symbol, peak, signal.ProtectedSL, stepLock);
                signal.ProtectedSL = stepLock;
            }
        }

        // ================================
        // 4. PROTECTED SL (INDEX T1 OR PARTIAL T1)
        // ================================
        // Gated on ProtectedSL alone, not IndexT1Hit: condition 3 arms it on an index-T1 touch and
        // condition 3b arms it on a partial-T1 premium touch, and either must be able to exit.
        if (signal.ProtectedSL > 0
            && optionLtp > 0
            && optionLtp <= signal.ProtectedSL
            && !BrokerStopCovers(signal))
        {
            return (
                "PROTECTED_SL_HIT",
                exitPrice);
        }



        // ================================
        // 5. OPTION STOPLOSS
        // ================================
        // SINGLE/HERO_ZERO: the broker-managed SL_M order owns this trigger now — ExitWorker no
        // longer fires a MARKET exit on crossing (that was the source of the slippage this whole
        // change exists to remove). The one exception is the bounded fallback below, gated on our
        // OWN tracked "no resting SL_M right now" state, not on price — it only exists to cover a
        // broker/API outage where PlaceStopLossOrderAsync keeps failing.
        // Spreads (BULL_SPREAD/BEAR_SPREAD) are unaffected — their OptionSL is a net two-leg
        // premium that can't be a single broker order, so they keep the original crossing check.
        if (triggerLtp > 0 && signal.OptionSL > 0)
        {
            if (!TradingConst.IsSingleLegStrategy(signal.StrategyType))
            {
                if (triggerLtp <= signal.OptionSL)
                {
                    return (
                        TradingConst.ExitReason.ForOptionStop(signal.OptionSL, signal.OptionLtp),
                        exitPrice);
                }
            }
            else if (triggerLtp <= signal.OptionSL
                     && signal.StopLossStatus != "PLACED")
            {
                var cfg = await _settingsService.GetAsync();
                // LastSlUpdateTime is stamped only when a stop was actually RESTING at the broker
                // (never on a failed attempt), so this measures real unprotected time. Falling back
                // to CreatedOn covers the case where placement failed on the very first attempt and
                // the field is still null -- previously a `LastSlUpdateTime.HasValue` guard skipped
                // the fallback entirely there, leaving a position that never had a stop with no
                // protection at all.
                var unprotectedSince = signal.LastSlUpdateTime ?? signal.CreatedOn;
                var noProtectionFor = IstClock.Now - unprotectedSince;
                if (noProtectionFor.TotalSeconds >= cfg.StopLossFallbackTimeoutSeconds)
                {
                    _logger.LogCritical(
                        "SL_M fallback MARKET exit firing for signal {Id} {Symbol} — no resting broker stop for {Secs}s (StopLossStatus={Status})",
                        signal.Id, signal.Symbol, (int)noProtectionFor.TotalSeconds, signal.StopLossStatus);
                    await _errorLogger.LogAsync(
                        "ExitWorker.SL_M_Fallback",
                        new Exception($"Broker SL_M unavailable for {(int)noProtectionFor.TotalSeconds}s — MARKET fallback fired"),
                        signal.Symbol, $"SignalId={signal.Id}");

                    return (
                        TradingConst.ExitReason.ForOptionStop(signal.OptionSL, signal.OptionLtp),
                        exitPrice);
                }
            }
        }




        // ================================
        // 6. INDEX STOPLOSS
        // Market structure failed
        // ================================

        if (underlyingLtp > 0)
        {
            if (signal.SignalType == TradingConst.SignalType.CALL
                && signal.StopLoss > 0
                && underlyingLtp <= signal.StopLoss)
            {
                return (
                    TradingConst.ExitReason.IndexSlHit,
                    exitPrice);
            }


            if (signal.SignalType == TradingConst.SignalType.PUT
                && signal.StopLoss > 0
                && underlyingLtp >= signal.StopLoss)
            {
                return (
                    TradingConst.ExitReason.IndexSlHit,
                    exitPrice);
            }
        }



        // ================================
        // 7L / 8L. TARGET LADDER (EnableTargetLadder, per symbol, off by default)
        // ================================
        // Replaces conditions 7 and 8 when on. R = original stop distance, derived from the stored
        // OptionT1 (T1 sits at AtrTarget1Multiplier / AtrStopLossMultiplier R), so no schema change.
        //   entry + TargetLadderFinalR x R (default 5) -> full exit of whatever is left
        //   entry + TargetLadderMidR   x R (default 3) -> sell 50% of the remaining lots (2 -> 1)
        //   OptionT1 (1R)                              -> sell 50% (4 -> 2), as condition 8 does
        // The old OptionT2 (1.3R) is ignored while the ladder is on, otherwise it would close the
        // runners before 3R. After any partial, the remaining lots are NOT closed at T1 - they run
        // to the next level, to breakeven (CompletePartialExitAsync moves OptionSL to entry) or to
        // the 15:15 force exit. A 1-lot trade keeps today's behaviour: full exit at T1.
        if (triggerLtp > 0
            && _strategySettings.GetBool("EnableTargetLadder", signal.Symbol, false))
        {
            var ladderCfg = await _settingsService.GetAsync();
            var levels = LadderLevels(
                signal,
                ladderCfg.AtrTarget1Multiplier,
                ladderCfg.AtrStopLossMultiplier,
                _strategySettings.GetDecimal("TargetLadderMidR", signal.Symbol, DefaultLadderMidR),
                _strategySettings.GetDecimal("TargetLadderFinalR", signal.Symbol, DefaultLadderFinalR));

            if (levels is { } lv)
            {
                var openLots = signal.RemainingQty > 0 ? signal.RemainingQty : signal.Quantity;
                var partialDone = signal.RemainingQty > 0 && signal.RemainingQty < signal.Quantity;

                switch (DecideLadder(triggerLtp, signal.OptionT1, lv.Mid, lv.Final, openLots, partialDone))
                {
                    case LadderAction.FinalExit:
                        return (TradingConst.ExitReason.OptionT5Hit, exitPrice);

                    case LadderAction.T1FullExit:
                        return (TradingConst.ExitReason.OptionT1Hit, exitPrice);

                    case LadderAction.SellAtT1:
                        await _positionClose.HandlePartialExitAsync(
                            signal, optionLtp, nowIst, chainBySymbol,
                            orders, journalRepo, orderRepo, uow, tradeCompletion);
                        return (null, 0);

                    case LadderAction.SellAtMid:
                        await _positionClose.HandlePartialExitAsync(
                            signal, optionLtp, nowIst, chainBySymbol,
                            orders, journalRepo, orderRepo, uow, tradeCompletion,
                            stage: 2, exitReason: TradingConst.ExitReason.PartialT3);
                        return (null, 0);

                    default:
                        return (null, 0);
                }
            }
        }

        // ================================
        // 7T. T2 PARTIAL (EnableT2Partial, per symbol, off by default; ignored while the ladder is on)
        // ================================
        // 3+ lots: T1 sells half (step 8), T2 sells half of the rest, the last lot is not closed at
        // T1/T2 - it exits via the stops (breakeven/step-lock/trailing) or the 15:15 force exit.
        if (triggerLtp > 0
            && !_strategySettings.GetBool("EnableTargetLadder", signal.Symbol, false)
            && _strategySettings.GetBool("EnableT2Partial", signal.Symbol, false))
        {
            var t2Open = signal.RemainingQty > 0 ? signal.RemainingQty : signal.Quantity;
            switch (DecideT2Partial(triggerLtp, signal.OptionT1, signal.OptionT2, t2Open, signal.Quantity))
            {
                case T2PartialAction.SellAtT1:
                    await _positionClose.HandlePartialExitAsync(
                        signal, optionLtp, nowIst, chainBySymbol,
                        orders, journalRepo, orderRepo, uow, tradeCompletion);
                    return (null, 0);

                case T2PartialAction.SellAtT2:
                    await _positionClose.HandlePartialExitAsync(
                        signal, optionLtp, nowIst, chainBySymbol,
                        orders, journalRepo, orderRepo, uow, tradeCompletion,
                        stage: 2, exitReason: TradingConst.ExitReason.PartialT2);
                    return (null, 0);

                case T2PartialAction.Hold:
                    return (null, 0);
            }
        }

        // ================================
        // 7. OPTION TARGET 2
        // Full exit
        // ================================

        if (triggerLtp > 0
            && signal.OptionT2 > 0
            && triggerLtp >= signal.OptionT2)
        {
            return (
                TradingConst.ExitReason.OptionT2Hit,
                exitPrice);
        }




        // ================================
        // 8. OPTION TARGET 1
        // Partial booking
        // ================================

        if (triggerLtp > 0
            && signal.OptionT1 > 0
            && triggerLtp >= signal.OptionT1)
        {
            var openLots =
                signal.RemainingQty > 0
                    ? signal.RemainingQty
                    : signal.Quantity;


            if (openLots > 1)
            {
                await _positionClose.HandlePartialExitAsync(
                    signal,
                    optionLtp,
                    nowIst,
                    chainBySymbol,
                    orders,
                    journalRepo,
                    orderRepo,
                    uow,
                    tradeCompletion);


                return (null, 0);
            }


            return (
                TradingConst.ExitReason.OptionT1Hit,
                exitPrice);
        }



        return (null, 0);
    }
}
