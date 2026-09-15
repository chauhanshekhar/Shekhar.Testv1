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
///   4. Protected SL         — only after Index T1
///   5. Option stop loss     — broker SL_M owns this for SINGLE/HERO_ZERO; the bounded MARKET
///                             fallback here covers a broker/API outage only
///   6. Index stop loss
///   7. Option Target2       — full exit
///   8. Option Target1       — partial booking when more than one lot is open
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

                if (lockProfit > 5)
                {
                    signal.ProtectedSL =
                        signal.OptionLtp + (lockProfit * 0.40m);
                }
                else
                {
                    signal.ProtectedSL =
                        signal.OptionLtp * 0.97m;
                }
            }
        }

        // ================================
        // 4. PROTECTED SL AFTER INDEX T1
        // 4. PROTECTED SL AFTER INDEX T1
        // ================================

        if (signal.IndexT1Hit
            && signal.ProtectedSL > 0
            && optionLtp > 0
            && optionLtp <= signal.ProtectedSL)
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
                        TradingConst.ExitReason.OptionSlHit,
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
                        TradingConst.ExitReason.OptionSlHit,
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
