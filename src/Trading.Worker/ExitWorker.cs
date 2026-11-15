using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Infrastructure;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;
using Trading.Worker.Exits;
using Trading.Worker.SignalGates;

namespace Trading.Worker;

/// <summary>
/// Exit pipeline for every open position, once per second while the market is open. This worker
/// owns the ORDER of the steps and nothing else — each step lives in its own service:
///
///   1. ExitDataLoader       — open signals, ticks, option chain, latest ATR (batched per cycle)
///   2. TrailingStopService  — MFE/MAE excursion + ATR trailing stop
///   3. StopLossSyncService  — broker-managed SL_M self-heal / trailing replace
///   4. ExitDecisionService  — the priority ladder that decides IF and WHY to exit
///   5. PositionCloseService — places the SELL (full close or T1 partial)
///
/// The save ordering in EvaluateExit is deliberate and load-bearing: excursion + trailing updates
/// are batched and committed BEFORE the exit decision runs, because that call can throw and this
/// scope's DbContext is discarded on the next cycle. Order-placement saves inside
/// PositionCloseService stay immediate — those must be durable before the broker call.
/// </summary>
public class ExitWorker : BackgroundService
{
    private readonly ILogger<ExitWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SystemSettingsService _settingsService;
    private readonly AppErrorLogger _errorLogger;

    public ExitWorker(
        ILogger<ExitWorker> logger,
        IServiceScopeFactory scopeFactory,
        SystemSettingsService settingsService,
        AppErrorLogger errorLogger)
    {
        _logger          = logger;
        _scopeFactory    = scopeFactory;
        _settingsService = settingsService;
        _errorLogger     = errorLogger;
    }

    // ── Main loop ─────────────────────────────────────────────────────────────
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowIst = TradingConstants.NowIst();

#if !TESTING
                if (nowIst.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                    || nowIst.TimeOfDay < MarketHours.SessionOpen
                    || nowIst.TimeOfDay > MarketHours.SessionClose)
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }
#endif
                await MonitorOpenSignals(nowIst, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExitWorker error");
                await _errorLogger.LogAsync("ExitWorker", ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }

    // ── Cycle orchestrator ────────────────────────────────────────────────────
    private async Task MonitorOpenSignals(DateTime nowIst, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var signalRepo  = sp.GetRequiredService<IRepository<Signal>>();
        var orderRepo   = sp.GetRequiredService<IRepository<Order>>();
        var tickRepo    = sp.GetRequiredService<IRepository<MarketTick>>();
        var journalRepo = sp.GetRequiredService<IRepository<TradeJournal>>();
        var optionRepo  = sp.GetRequiredService<IRepository<OptionChainData>>();
        var snapshotRepo = sp.GetRequiredService<IRepository<IndicatorSnapshot>>();
        var trailingLogRepo = sp.GetRequiredService<IRepository<TrailingStopLog>>();
        var paperRepo   = sp.GetRequiredService<IRepository<PaperTrade>>();
        var paperTradingService = sp.GetRequiredService<PaperTradingService>();
        var tradeCompletion = sp.GetRequiredService<TradeCompletionService>();
        var orders      = sp.GetRequiredService<IOrderService>();
        var liveData    = sp.GetRequiredService<IGrowwLiveDataService>();
        var instruments = sp.GetRequiredService<IInstrumentCache>();
        var uow         = sp.GetRequiredService<IUnitOfWork>();

        var trailingStops = sp.GetRequiredService<TrailingStopService>();
        var stopLossSync  = sp.GetRequiredService<StopLossSyncService>();
        var exitDecision  = sp.GetRequiredService<ExitDecisionService>();
        var positionClose = sp.GetRequiredService<PositionCloseService>();

        var cfg           = await _settingsService.GetAsync(ct);
        var todayStartUtc = TradingConstants.TodayUtcStart();

        // Step 1: load open positions FIRST
        var openSignals = await ExitDataLoader.LoadOpenSignalsAsync(
            signalRepo,
            journalRepo,
            todayStartUtc);

        // no open trade then nothing to exit
        if (openSignals.Count == 0)
            return;

        // Step 2: check daily loss
        //
        // NOTE: the result is deliberately NOT acted on — existing positions must still be
        // protected once the daily loss cap is hit, so this must never return early here. The
        // call is preserved exactly as it was, but nothing reads `dailyLossHit`, which means the
        // journal + paper-trade + tick queries behind it run every second and are discarded.
        // Either wire it into a decision or drop the call; both are behaviour changes, so
        // neither was made as part of this extraction.
        var dailyLossHit = await ExitDataLoader.IsDailyLossBreachedAsync(
            journalRepo,
            paperRepo,
            tickRepo,
            cfg.MaxLossPerDay,
            todayStartUtc,
            ct);

        // Live option premiums for the open positions — one targeted quote each, refreshed every
        // cycle. The chain snapshot below is only 30s-granular and may be up to 2 minutes stale,
        // which left MFE/MAE and the OptionSL/T1/T2 checks running on old prices.
        var liveOptionQuotes = await ExitDataLoader.LoadLiveOptionQuotesAsync(
            liveData, instruments, openSignals, _logger, ct);

        // Step 3: load latest ticks + option chain + latest ATR per symbol
        var ltpBySymbol   = await ExitDataLoader.LoadLatestTicksAsync(
            tickRepo, openSignals.Select(s => s.Symbol));
        var chainBySymbol = await ExitDataLoader.LoadOptionChainSnapshotAsync(optionRepo);
        var latestAtrBySymbol = await ExitDataLoader.LoadLatestAtrBySymbolAsync(
            snapshotRepo, openSignals.Select(s => s.Symbol).Distinct().ToList());

        var isForceExit = MarketHours.IsForceExitWindow();
        if (chainBySymbol.Count == 0 && !isForceExit) return;

        // Step 4: evaluate each open signal
        foreach (var signal in openSignals)
        {
            try
            {
                await EvaluateExit(
                    signal,
                    ltpBySymbol,
                    chainBySymbol,
                    latestAtrBySymbol,
                    trailingLogRepo,
                    signalRepo,
                    isForceExit,
                    nowIst,
                    orders,
                    paperTradingService,
                    journalRepo,
                    orderRepo,
                    uow,
                    tradeCompletion,
                    liveOptionQuotes,
                    trailingStops,
                    stopLossSync,
                    exitDecision,
                    positionClose);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExitWorker error on signal {Id}", signal.Id);
                await _errorLogger.LogAsync("ExitWorker", ex, signal.Symbol, $"SignalId={signal.Id}");
            }
        }
    }

    /// <summary>
    /// True when a spread can't be evaluated this cycle because its second leg has no price.
    ///
    /// Must key off IsSingleLegStrategy, NOT `!= SINGLE`: HERO_ZERO is also single-leg, so its
    /// leg2 price is permanently 0. Testing `!= SINGLE` therefore matched every HERO_ZERO signal
    /// and returned before the excursion update, the trailing stop and the entire exit ladder —
    /// so HeroZero positions ran completely unmanaged (no T1, no T2, no option SL) and the only
    /// order they ever got was the SL_M placed at entry.
    /// </summary>
    internal static bool ShouldSkipMissingLeg2(string strategyType, decimal leg2Ltp, bool forceExit)
        => !TradingConst.IsSingleLegStrategy(strategyType)
           && leg2Ltp == 0
           && !forceExit;

    /// <summary>
    /// True while the entry BUY has not been confirmed filled (OrderPlaced). OptionSL/T1/T2 are
    /// still anchored to the quote the signal was built on, and ActivateSignalAsync re-anchors them
    /// to the fill by RATIO. Trailing, step-lock, breakeven or target logic run before that point
    /// both fire against the wrong entry and corrupt the ratio (signal 10291: stop trailed 67 -> 83
    /// on a stale 82.80 quote, then re-anchored to 109.88 on a 109.80 fill). Force exit still runs.
    /// </summary>
    internal static bool ShouldSkipUnfilledEntry(string status, bool forceExit)
        => status == SignalStatus.OrderPlaced && !forceExit;

    /// <summary>
    /// True while a paper partial-exit SELL awaits its fill (PartialExitPending). Booking a full
    /// paper exit then double-counts the partial lots, and the later partial fill reset the
    /// signal to ACTIVE after it closed (signal 10316, 2026-11-02).
    /// </summary>
    internal static bool ShouldDeferPaperExit(string status)
        => status == SignalStatus.PartialExitPending;

    // ── Evaluate a single signal ─────────────────────────────────────────────
    private async Task EvaluateExit(
        Signal signal,
        Dictionary<string, decimal> ltpBySymbol,
        Dictionary<string, List<OptionChainData>> chainBySymbol,
        Dictionary<string, decimal> latestAtrBySymbol,
        IRepository<TrailingStopLog> trailingLogRepo,
        IRepository<Signal> signalRepo,
        bool forceExit,
        DateTime nowIst,
        IOrderService orders,
        PaperTradingService paperTradingService,
        IRepository<TradeJournal> journalRepo,
        IRepository<Order> orderRepo,
        IUnitOfWork uow,
        TradeCompletionService tradeCompletion,
        Dictionary<string, decimal> liveOptionQuotes,
        TrailingStopService trailingStops,
        StopLossSyncService stopLossSync,
        ExitDecisionService exitDecision,
        PositionCloseService positionClose)
    {
        if (ShouldSkipUnfilledEntry(signal.Status, forceExit))
            return;

        if (!ltpBySymbol.TryGetValue(signal.Symbol, out var underlyingLtp))
            return;

        var (optionLtp, leg2CurrentLtp, currentDelta) =
            ExitDataLoader.ResolveLtps(signal, chainBySymbol, liveOptionQuotes);

        if (ShouldSkipMissingLeg2(signal.StrategyType, leg2CurrentLtp, forceExit))
        {
            return;
        }

        var triggerLtp =
            PositionTrigger.CurrentValue(
                signal,
                optionLtp,
                leg2CurrentLtp);

        var entryTriggerValue =
            PositionTrigger.EntryValue(signal);

        // Batch the excursion + trailing-SL updates into one save, and persist it BEFORE
        // the exit decision runs — that call can throw (e.g. from the partial-exit path's
        // DB lookups ahead of the broker call), and this scope's DbContext is discarded on
        // the next cycle, so anything not saved yet would be silently lost rather than
        // just delayed. Order-placement saves (PositionCloseService) stay immediate since
        // those must be durable before the broker call.
        var hasChanges = false;

        // Captured BEFORE UpdateExcursion mutates signal.MfePrice — after that call, a
        // fresh high and "no change" both leave optionLtp == signal.MfePrice, so this can't
        // be recovered from the post-update state.
        var mfeIncreased = optionLtp > signal.MfePrice;

        hasChanges |= TrailingStopService.UpdateExcursion(
            signal,
            optionLtp);

        hasChanges |= await trailingStops.UpdateAsync(
            signal,
            triggerLtp,
            entryTriggerValue,
            underlyingLtp,
            optionLtp,
            currentDelta,
            latestAtrBySymbol,
            mfeIncreased,
            trailingLogRepo);

        if (hasChanges)
        {
            await uow.SaveChangesAsync();
        }

        // Broker-managed SL_M sync — SINGLE/HERO_ZERO live signals only. Self-heals a missing
        // placement and replaces the resting order whenever OptionSL moved this cycle (ATR
        // trailing, or TradeCompletionService.CompletePartialExitAsync resetting it to breakeven
        // after a partial exit — both mutate the same field). Spreads keep the polling-based
        // OPTION_SL_HIT exit below since their OptionSL is a net two-leg premium.
        // Skipped entirely on a force-exit cycle: re-arming a resting stop minutes before EOD
        // shutdown has no protective value, and it actively blocks the exit — ClosePositionAsync
        // must cancel the resting SL_M before its MARKET sell, so a stop placed here moments
        // earlier is exactly what that cancel then fails on (broker rejects the cancel of an
        // order it is itself squaring off), aborting the close every cycle until 15:30.
        var isLiveSignal = !orders.IsPaperSignal(signal);
        if (isLiveSignal && TradingConst.IsSingleLegStrategy(signal.StrategyType) && !forceExit)
        {
            await stopLossSync.SyncAsync(signal, orders, orderRepo, signalRepo, uow, optionLtp);
        }

        // save protection old values
        var oldIndexT1Hit = signal.IndexT1Hit;
        var oldProtectedSL = signal.ProtectedSL;

        var (exitReason, exitPrice) =
            await exitDecision.DetermineAsync(
                signal,
                triggerLtp,
                optionLtp,
                leg2CurrentLtp,
                underlyingLtp,
                forceExit,
                nowIst,
                chainBySymbol,
                orders,
                journalRepo,
                orderRepo,
                uow,
                tradeCompletion);

        // save only protection changes
        if (signal.IndexT1Hit != oldIndexT1Hit ||
            signal.ProtectedSL != oldProtectedSL)
        {
            await uow.SaveChangesAsync();
        }

        if (exitReason == null)
            return;

        // ==================================================
        // PAPER TRADING
        // ==================================================
        if (!isLiveSignal)
        {
            // A partial SELL is in flight; BrokerOrderSyncWorker must settle it first, or the
            // full exit books the same lots twice and the partial fill reopens the signal (10316).
            if (ShouldDeferPaperExit(signal.Status))
                return;

            var fakeExitOrder = new Order
            {
                Id = 0,
                SignalId = signal.Id,
                Side = "SELL",
                Symbol = signal.OptionSymbol,
                AvgPrice = exitPrice,
                FilledQty = signal.RemainingQty > 0
                    ? signal.RemainingQty
                    : signal.Quantity,
                Status = OrderStatus.Complete,
                OrderedAt = IstClock.Now,
                UpdatedAt = IstClock.Now
            };

            await tradeCompletion.CompleteExitAsync(
                signal,
                fakeExitOrder,
                exitReason);

            await paperTradingService.CloseBySymbolAsync(
                signal.OptionSymbol,
                exitPrice,
                exitReason,
                leg2CurrentLtp);

            return;
        }

        // ==================================================
        // LIVE TRADING
        // ==================================================
        // Send SELL only.
        // Do NOT create journal.
        // BrokerOrderSyncWorker handles COMPLETE.
        // ==================================================
        var exitOrderId =
            await positionClose.ClosePositionAsync(
                signal,
                exitReason,
                orders,
                orderRepo,
                uow,
                forceExit);

        if (exitOrderId == null)
        {
            _logger.LogError(
                "Live exit order failed Signal {Id}",
                signal.Id);

            return;
        }
    }
}
