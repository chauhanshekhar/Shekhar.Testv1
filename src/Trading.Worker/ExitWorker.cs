using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;
using Trading.Worker.SignalGates;

namespace Trading.Worker;

public class ExitWorker : BackgroundService
{
    private readonly ILogger<ExitWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SystemSettingsService _settingsService;
    private readonly IInstrumentCache _instruments;
    private readonly AppErrorLogger _errorLogger;


    public ExitWorker(
        ILogger<ExitWorker> logger,
        IServiceScopeFactory scopeFactory,
        SystemSettingsService settingsService,
        IInstrumentCache instruments,
        AppErrorLogger errorLogger)
    {
        _logger          = logger;
        _scopeFactory    = scopeFactory;
        _settingsService = settingsService;
        _instruments     = instruments;
        _errorLogger     = errorLogger;
    }

    // ── Main loop ─────────────────────────────────────────────────────────────
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ExitWorker started");

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
        var positionRepo = sp.GetRequiredService<IRepository<Position>>();
        var tickRepo    = sp.GetRequiredService<IRepository<MarketTick>>();
        var journalRepo = sp.GetRequiredService<IRepository<TradeJournal>>();
        var optionRepo  = sp.GetRequiredService<IRepository<OptionChainData>>();
        var outcomeRepo = sp.GetRequiredService<IRepository<SignalOutcome>>();
        var paperRepo   = sp.GetRequiredService<IRepository<PaperTrade>>();
        var paperTradingService = sp.GetRequiredService<PaperTradingService>();
        var tradeCompletion =
    sp.GetRequiredService<TradeCompletionService>();
        var orders      = sp.GetRequiredService<IOrderService>();
        var uow         = sp.GetRequiredService<IUnitOfWork>();

        var cfg           = await _settingsService.GetAsync(ct);
        var todayStartUtc = TradingConstants.TodayUtcStart();

        // Step 1: load open positions FIRST
        var openSignals = await LoadOpenSignals(
            signalRepo,
            journalRepo,
            todayStartUtc);


        // no open trade then nothing to exit
        if (openSignals.Count == 0)
            return;


        // Step 2: check daily loss
        var dailyLossHit = await IsDailyLossBreached(
            journalRepo,
            paperRepo,
            tickRepo,
            cfg.MaxLossPerDay,
            todayStartUtc,
            ct);


        // Do NOT return here
        // Existing positions must be protected
        if (dailyLossHit)
        {
            _logger.LogWarning(
                "Daily loss breached but monitoring {Count} open trades for exit",
                openSignals.Count);
        }
        if (openSignals.Count == 0) return;

        // Step 3: load latest ticks + option chain
        var ltpBySymbol   = await LoadLatestTicks(tickRepo);
        var chainBySymbol = await LoadOptionChainSnapshot(optionRepo);

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
     isForceExit,
     nowIst,
     orders,
     paperTradingService,
     journalRepo,
     orderRepo,
     positionRepo,
     outcomeRepo,
     uow,
     tradeCompletion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExitWorker error on signal {Id}", signal.Id);
                await _errorLogger.LogAsync("ExitWorker", ex, signal.Symbol, $"SignalId={signal.Id}");
            }
        }
    }

    // ── Step 1: Daily loss limit check ────────────────────────────────────────
    private async Task<bool> IsDailyLossBreached(
        IRepository<TradeJournal> journalRepo,
        IRepository<PaperTrade> paperRepo,
        IRepository<MarketTick> tickRepo,
        decimal maxDailyLoss,
        DateTime todayStartUtc,
        CancellationToken ct)
    {
        var tomorrowStart = todayStartUtc.AddDays(1);
        var todayJournal  = await journalRepo.FindAsync(
            j => j.TradeDate >= todayStartUtc && j.TradeDate < tomorrowStart);
        var realizedPnl = todayJournal.Sum(x => x.ProfitLoss);

        if (realizedPnl <= -maxDailyLoss)
        {
            _logger.LogWarning("Daily loss limit ₹{Limit:F0} reached (realized ₹{Net:F0})", maxDailyLoss, realizedPnl);
            return true;
        }

        var unrealizedPnl = await ComputeUnrealizedPnl(paperRepo, tickRepo);
        if (realizedPnl + unrealizedPnl <= -maxDailyLoss)
        {
            _logger.LogWarning("Daily loss limit ₹{Limit:F0} reached — realized ₹{R:F0} + unrealized −₹{U:F0}",
                maxDailyLoss, realizedPnl, Math.Max(0m, -unrealizedPnl));
            return true;
        }

        return false;
    }

    // ── Step 1a: Unrealized P&L from open paper trades ────────────────────────
    private async Task<decimal> ComputeUnrealizedPnl(
        IRepository<PaperTrade> paperRepo,
        IRepository<MarketTick> tickRepo)
    {
        var cutoff       = IstClock.Now.AddSeconds(-30);
        var ltpBySymbol  = (await tickRepo.FindAsync(t => t.TickTime >= cutoff))
            .GroupBy(t => t.Symbol)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TickTime).First().LastPrice);

        var openTrades   = await paperRepo.FindAsync(p => p.Status == "OPEN");
        var unrealized   = 0m;

        foreach (var p in openTrades)
        {
            var sym = !string.IsNullOrEmpty(p.OptionSymbol) ? p.OptionSymbol : p.Symbol;
            if (!ltpBySymbol.TryGetValue(sym, out var currentLtp)) continue;

            var side   = p.Side?.Equals("CALL", StringComparison.OrdinalIgnoreCase) == true ? 1m : -1m;
            unrealized += (currentLtp - p.EntryPrice) * p.Quantity * side;

            if (!string.IsNullOrEmpty(p.Leg2OptionSymbol) && p.Leg2EntryPrice > 0
                && ltpBySymbol.TryGetValue(p.Leg2OptionSymbol, out var leg2Ltp))
            {
                // Leg2 is SHORT option for spreads.
                // Profit when Leg2 premium falls.
                unrealized +=
                    (p.Leg2EntryPrice - leg2Ltp) * p.Quantity;
            }
        }

        return unrealized;
    }

    // ── Step 2: Load open signals ─────────────────────────────────────────────
    private static async Task<List<Signal>> LoadOpenSignals(
        IRepository<Signal> signalRepo,
        IRepository<TradeJournal> journalRepo,
        DateTime todayStartUtc)
    {
        var todaySignals = await signalRepo.FindAsync(s => s.CreatedOn >= todayStartUtc);

        var closedIds = (await journalRepo.FindAsync(
                j => j.TradeDate >= todayStartUtc && j.SignalId.HasValue && !j.IsPartial))
            .Select(j => j.SignalId!.Value)
            .ToHashSet();
        return todaySignals
     .Where(s =>
      (
          s.Status == SignalStatus.OrderPlaced ||
          s.Status == SignalStatus.Active ||
          s.Status == SignalStatus.PartialExitPending
      )
      && !closedIds.Contains(s.Id))
     .ToList();
    }

    // ── Step 3a: Load latest ticks ────────────────────────────────────────────
    private static async Task<Dictionary<string, decimal>> LoadLatestTicks(
        IRepository<MarketTick> tickRepo)
    {
        var cutoff = IstClock.Now.AddSeconds(-30);
        return (await tickRepo.FindAsync(t => t.TickTime >= cutoff))
            .GroupBy(t => t.Symbol)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TickTime).First().LastPrice);
    }

    // ── Step 3b: Load latest option chain snapshot ────────────────────────────
    private static async Task<Dictionary<string, List<OptionChainData>>> LoadOptionChainSnapshot(
        IRepository<OptionChainData> optionRepo)
    {
        var latestSnapshotId = (await optionRepo.FindAsync(
                o => o.CreatedOn >= IstClock.Now.AddMinutes(-2)))
            .OrderByDescending(x => x.CreatedOn)
            .Select(x => x.SnapshotId)
            .FirstOrDefault();

        if (string.IsNullOrEmpty(latestSnapshotId))
            return new Dictionary<string, List<OptionChainData>>();

        return (await optionRepo.FindAsync(o => o.SnapshotId == latestSnapshotId))
            .GroupBy(o => o.Symbol)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    // ── Step 4: Evaluate single signal ───────────────────────────────────────
    private static (decimal optionLtp, decimal leg2Ltp) ResolveLtps(
        Signal signal,
        Dictionary<string, List<OptionChainData>> chainBySymbol)
    {
        decimal optionLtp = 0;
        decimal leg2Ltp = 0;

        if (chainBySymbol.TryGetValue(signal.Symbol, out var chain))
        {
            optionLtp =
                chain.FirstOrDefault(x =>
                    x.TradingSymbol == signal.OptionSymbol)
                ?.Ltp ?? 0;


            if (signal.StrategyType != TradingConst.StrategyType.Single)
            {
                leg2Ltp =
                    chain.FirstOrDefault(x =>
                        x.OptionType == signal.Leg2OptionType &&
                        x.StrikePrice == signal.Leg2StrikePrice)
                    ?.Ltp ?? 0;
            }
        }
        return (optionLtp, leg2Ltp);
    }
    private async Task EvaluateExit(
       Signal signal,
       Dictionary<string, decimal> ltpBySymbol,
       Dictionary<string, List<OptionChainData>> chainBySymbol,
       bool forceExit,
       DateTime nowIst,
       IOrderService orders,
       PaperTradingService paperTradingService,
       IRepository<TradeJournal> journalRepo,
       IRepository<Order> orderRepo,
       IRepository<Position> positionRepo,
       IRepository<SignalOutcome> outcomeRepo,
       IUnitOfWork uow,
       TradeCompletionService tradeCompletion)
    {
        if (!ltpBySymbol.TryGetValue(signal.Symbol, out var underlyingLtp))
            return;


        var (optionLtp, leg2CurrentLtp) =
            ResolveLtps(signal, chainBySymbol);


        if (signal.StrategyType != TradingConst.StrategyType.Single
            && leg2CurrentLtp == 0
            && !forceExit)
        {
            _logger.LogWarning(
                "Leg2 LTP unavailable for {Symbol} signal {Id}",
                signal.Symbol,
                signal.Id);

            return;
        }


        var triggerLtp =
            PositionTrigger.CurrentValue(
                signal,
                optionLtp,
                leg2CurrentLtp);


        var entryTriggerValue =
            PositionTrigger.EntryValue(signal);



        // Accumulate scalar field changes across this evaluation and persist once at the
        // end, instead of one SaveChangesAsync per update — reduces DB writes per signal
        // per monitoring cycle. Order-placement saves (ClosePosition/HandlePartialExit)
        // stay immediate since those must be durable before the broker call.
        var hasChanges = false;

        hasChanges |= await UpdateExcursion(
            signal,
            optionLtp);


        hasChanges |= await UpdateTrailingSL(
            signal,
            triggerLtp,
            entryTriggerValue,
            underlyingLtp,
            optionLtp);




        // save protection old values

        var oldIndexT1Hit =
            signal.IndexT1Hit;

        var oldProtectedSL =
            signal.ProtectedSL;


        var (exitReason, exitPrice) =
            await DetermineExitReason(
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

        hasChanges |= signal.IndexT1Hit != oldIndexT1Hit ||
                      signal.ProtectedSL != oldProtectedSL;

        if (hasChanges)
        {
            await uow.SaveChangesAsync();
        }



        if (exitReason == null)
            return;



        // ==================================================
        // PAPER TRADING
        // ==================================================
        var isLive =
       orders.CurrentMode != TradingMode.Paper;
        if (!isLive)
        {
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


            _logger.LogInformation(
                "Paper exit completed Signal {Id}",
                signal.Id);


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
            await ClosePosition(
                signal,
                exitReason,
                orders,
                orderRepo,
                uow);


        if (exitOrderId == null)
        {
            _logger.LogError(
                "Live exit order failed Signal {Id}",
                signal.Id);

            return;
        }


        _logger.LogInformation(
            "Live exit order submitted Signal={SignalId} Order={OrderId}",
            signal.Id,
            exitOrderId);
    }

    // ── 4b: MFE / MAE update ─────────────────────────────────────────────────
    // Returns whether a field changed — caller batches the save with other updates.
    private static Task<bool> UpdateExcursion(Signal signal, decimal optionLtp)
    {
        if (optionLtp <= 0) return Task.FromResult(false);
        var changed = false;
        if (optionLtp > signal.MfePrice) { signal.MfePrice = optionLtp; changed = true; }
        if (signal.MaePrice == 0 || optionLtp < signal.MaePrice) { signal.MaePrice = optionLtp; changed = true; }
        return Task.FromResult(changed);
    }

    // ── 4c: Trailing SL ──────────────────────────────────────────────────────
    // Returns whether a field changed — caller batches the save with other updates.
    private async Task<bool> UpdateTrailingSL(
        Signal signal,
        decimal triggerLtp,
        decimal entryTriggerValue,
        decimal underlyingLtp,
        decimal optionLtp)
    {
        var cfg     = await _settingsService.GetAsync();
        var updated = false;

        if (triggerLtp > 0 && entryTriggerValue > 0
            && triggerLtp > entryTriggerValue * (1m + cfg.TrailSLActivationPct / 100m))
        {
            var profitPct = (triggerLtp - entryTriggerValue) / entryTriggerValue * 100m;

            decimal newOptionSL;
            if (signal.StrategyType == TradingConst.StrategyType.HeroZero)
            {
                // Hero Zero: premiums can run 100-300% — tighter trail locks in gains
                newOptionSL = profitPct switch
                {
                    >= 200 => triggerLtp * 0.85m,   // lock 85% at 200%+ profit
                    >= 100 => triggerLtp * 0.75m,   // lock 75% at 100%+ profit
                    >= 50  => triggerLtp * 0.65m,   // lock 65% at 50%+ profit
                    _      => triggerLtp * 0.55m,   // lock 55% once activated
                };
            }
            else
            {
                newOptionSL = profitPct switch
                {
                    >= 50 => triggerLtp * cfg.TrailSLAt50PctProfit,
                    >= 30 => triggerLtp * cfg.TrailSLAt30PctProfit,
                    _     => triggerLtp * cfg.TrailSLDefault,
                };
            }

            if (newOptionSL > signal.OptionSL) { signal.OptionSL = Math.Round(newOptionSL, 1); updated = true; }
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

    // ── 4d: Determine exit reason ─────────────────────────────────────────────
    private async Task<(string? exitReason, decimal exitPrice)> DetermineExitReason(
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
            _logger.LogWarning(
                "Hero-Zero detected premium {Ltp} {Symbol}",
                optionLtp,
                signal.Symbol);

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
            && optionLtp > 0)
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


                _logger.LogInformation(
                    "Index T1 reached {Symbol}. Protected SL moved to {SL}",
                    signal.Symbol,
                    signal.ProtectedSL);
            }
        }



        // ================================
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

        if (triggerLtp > 0
            && signal.OptionSL > 0
            && triggerLtp <= signal.OptionSL)
        {
            return (
                TradingConst.ExitReason.OptionSlHit,
                exitPrice);
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
                await HandlePartialExit(
     signal,
     optionLtp,
     nowIst,
     chainBySymbol,
     orders,
     journalRepo,
     orderRepo,
     uow, tradeCompletion);


                return (null, 0);
            }


            return (
                TradingConst.ExitReason.OptionT1Hit,
                exitPrice);
        }



        return (null, 0);
    }

    // ── 4e: Place close order + update signal status ──────────────────────────
    // Mirrors the entry flow's Order-row-first pattern: the exit Order row is inserted and
    // saved BEFORE calling the broker, so a crash/timeout during close is still visible
    // (Status=PENDING) rather than lost. Returns the exit Order's Id on success, or null.
    private async Task<long?> ClosePosition(
        Signal signal,
        string exitReason,
        IOrderService orders,
        IRepository<Order> orderRepo,
        IUnitOfWork uow)
    {
        var finalLots = signal.RemainingQty > 0
            ? signal.RemainingQty
            : signal.PartialPnL == 0 ? signal.Quantity : 0;

        if (finalLots <= 0)
        {
            _logger.LogWarning("No open lots for signal {Id} {Symbol} — skipping close", signal.Id, signal.Symbol);
            return null;
        }

        var exitOrder = new Order
        {
            SignalId = signal.Id,
            OrderReferenceId = Guid.NewGuid().ToString("N"),
            Broker = "GROWW",
            Symbol = !string.IsNullOrWhiteSpace(signal.OptionSymbol) ? signal.OptionSymbol : signal.Symbol,
            Exchange = signal.Exchange,
            Side = "SELL",
            OrderType = "MARKET",
            ProductType = "MIS",
            Status = OrderStatus.Pending,
            Quantity = finalLots,
            OrderedAt = IstClock.Now,
            UpdatedAt = IstClock.Now,
        };
        await orderRepo.StageAsync(exitOrder);
        await uow.SaveChangesAsync();

        string closeOrderId;
        try { closeOrderId = await orders.ClosePositionAsync(signal, exitReason, finalLots); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Close order FAILED for signal {Id} {Symbol}", signal.Id, signal.Symbol);
            signal.Status = SignalStatus.CloseFailed;
            exitOrder.Status = OrderStatus.Failed;
            exitOrder.StatusMessage = ex.Message;
            exitOrder.UpdatedAt = IstClock.Now;
            await uow.SaveChangesAsync();
            return null;
        }

        if (closeOrderId == "CLOSE_FAILED")
        {
            _logger.LogError("Broker rejected close for signal {Id} {Symbol}", signal.Id, signal.Symbol);
            signal.Status = SignalStatus.CloseFailed;
            exitOrder.Status = OrderStatus.Failed;
            exitOrder.UpdatedAt = IstClock.Now;
            await uow.SaveChangesAsync();
            return null;
        }

        // Groww accepted the close — fill status isn't known yet, stays PENDING for
        // BrokerOrderSyncWorker to confirm, same as the entry flow.
        exitOrder.BrokerOrderId = closeOrderId;
        exitOrder.Status = OrderStatus.Pending;
        exitOrder.UpdatedAt = IstClock.Now;

        signal.Status = SignalStatus.ExitPending;
        signal.ExitReason = exitReason;

        await uow.SaveChangesAsync();

        return exitOrder.Id;
    }

    // ── 4f: Write TradeJournal + SignalOutcome ────────────────────────────────



    // ── Partial exit (T1 hit, multiple lots) ──────────────────────────────────
    private async Task HandlePartialExit(
        Signal signal,
        decimal optionLtp,
        DateTime nowIst,
        Dictionary<string, List<OptionChainData>> chainBySymbol,
        IOrderService orders,
        IRepository<TradeJournal> journalRepo,
        IRepository<Order> orderRepo,
        IUnitOfWork uow,
        TradeCompletionService tradeCompletion)
    {
     var alreadyPartial =
 await journalRepo.FirstOrDefaultAsync(
 j => j.SignalId == signal.Id && j.IsPartial);
        if (alreadyPartial != null) return;


        var initialQty  = signal.RemainingQty > 0 ? signal.RemainingQty : signal.Quantity;
        var closeLots   = Math.Max((int)Math.Ceiling(initialQty * 0.50m), 1);
        var lotSize     = _instruments.GetLotSize(signal.Symbol);

        var partialOrder = new Order
        {
            SignalId = signal.Id,
            OrderReferenceId = Guid.NewGuid().ToString("N"),
            Broker = "GROWW",
            Symbol = !string.IsNullOrWhiteSpace(signal.OptionSymbol) ? signal.OptionSymbol : signal.Symbol,
            Exchange = signal.Exchange,
            Side = "SELL",
            OrderType = "MARKET",
            ProductType = "MIS",
            Status = OrderStatus.Pending,
            Quantity = closeLots,
            OrderedAt = IstClock.Now,
            UpdatedAt = IstClock.Now,
        };
        await orderRepo.StageAsync(partialOrder);
        await uow.SaveChangesAsync();

        string partialOrderId;
        try { partialOrderId = await orders.ClosePositionAsync(signal, TradingConst.ExitReason.OptionT1Hit, closeLots); }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Partial close FAILED for signal {Id}", signal.Id);
            partialOrder.Status = OrderStatus.Failed;
            partialOrder.StatusMessage = ex.Message;
            partialOrder.UpdatedAt = IstClock.Now;
            await uow.SaveChangesAsync();
            return;
        }

        if (partialOrderId == "CLOSE_FAILED")
        {
            _logger.LogError("Broker rejected partial close for signal {Id}", signal.Id);
            partialOrder.Status = OrderStatus.Failed;
            partialOrder.UpdatedAt = IstClock.Now;
            await uow.SaveChangesAsync();
            return;
        }

        partialOrder.BrokerOrderId = partialOrderId;
        partialOrder.UpdatedAt = IstClock.Now;

        var entryOrder = (await orderRepo.FindAsync(o => o.SignalId == signal.Id && o.Side == "BUY"))
            .OrderBy(o => o.OrderedAt)
            .FirstOrDefault();

        //    var partialPnl = (optionLtp - signal.OptionLtp) * closeLots * lotSize
        //                   + tradeCompletion.ComputeLeg2Pnl(
        //signal,
        //closeLots,
        //lotSize,
        //chainBySymbol);

        //    signal.PartialPnL   += partialPnl;
        //    signal.RemainingQty  = initialQty - closeLots;
        //    signal.OptionSL      = PositionTrigger.EntryValue(signal);  // move SL to breakeven
        //    await uow.SaveChangesAsync();

        //    await journalRepo.AddAsync(new TradeJournal
        //    {
        //        Symbol            = signal.Symbol,
        //        TradeType         = signal.SignalType,
        //        EntryPrice        = signal.OptionLtp,
        //        ExitPrice         = optionLtp,
        //        Quantity          = closeLots,
        //        ProfitLoss        = partialPnl,
        //        TradeDate         = nowIst,
        //        Remarks           = $"PARTIAL_{signal.Id}",
        //        SignalId          = signal.Id,
        //        EntryOrderId      = entryOrder?.Id,
        //        ExitOrderId       = partialOrder.Id,
        //        IsPartial         = true,
        //        StrategyType      = signal.StrategyType,
        //        ExitReason        = TradingConst.ExitReason.OptionT1Hit,
        //        SignalConfidence  = signal.Confidence,
        //        RsiAtEntry        = signal.RsiAtEntry,
        //        PcrAtEntry        = signal.PcrAtEntry,
        //        DeltaAtEntry      = signal.DeltaAtEntry,
        //        ChangeInOiAtEntry = signal.ChangeInOiAtEntry,
        //    });
        //    await uow.SaveChangesAsync();
        partialOrder.BrokerOrderId = partialOrderId;
        partialOrder.Status = OrderStatus.Pending;
        partialOrder.UpdatedAt = IstClock.Now;


        // mark waiting for broker confirmation
        signal.Status = SignalStatus.PartialExitPending;


        await uow.SaveChangesAsync();


        _logger.LogInformation(
            "Partial exit submitted Signal={Id} Order={Order}",
            signal.Id,
            partialOrderId);

        _logger.LogInformation("T1 PARTIAL EXIT — {Lots} lots @ ₹{Ltp:F1} | SL→BE ₹{BE:F1} | {Symbol}",
            closeLots, optionLtp, signal.OptionLtp, signal.Symbol);
    }
}
