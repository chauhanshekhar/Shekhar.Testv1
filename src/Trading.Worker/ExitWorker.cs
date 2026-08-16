using Trading.Application.Interfaces;
using Trading.Application.Models;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Brokers.Groww;
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
    private readonly StopLossOrderCoordinator _stopLossCoordinator;

    // Ignore trailing-SL moves smaller than this — avoids a DB write every ~1s cycle for
    // sub-rupee noise (55.1 → 55.2 → 55.3 ...); only commits once the move is meaningful.
    private const decimal MinTrailingSlMovement = 0.5m;

    public ExitWorker(
        ILogger<ExitWorker> logger,
        IServiceScopeFactory scopeFactory,
        SystemSettingsService settingsService,
        IInstrumentCache instruments,
        AppErrorLogger errorLogger,
        StopLossOrderCoordinator stopLossCoordinator)
    {
        _logger          = logger;
        _scopeFactory    = scopeFactory;
        _settingsService = settingsService;
        _instruments     = instruments;
        _errorLogger     = errorLogger;
        _stopLossCoordinator = stopLossCoordinator;
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
        var positionRepo = sp.GetRequiredService<IRepository<Position>>();
        var tickRepo    = sp.GetRequiredService<IRepository<MarketTick>>();
        var journalRepo = sp.GetRequiredService<IRepository<TradeJournal>>();
        var optionRepo  = sp.GetRequiredService<IRepository<OptionChainData>>();
        var outcomeRepo = sp.GetRequiredService<IRepository<SignalOutcome>>();
        var snapshotRepo = sp.GetRequiredService<IRepository<IndicatorSnapshot>>();
        var trailingLogRepo = sp.GetRequiredService<IRepository<TrailingStopLog>>();
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
        if (openSignals.Count == 0) return;

        // Step 3: load latest ticks + option chain + latest ATR per symbol
        var ltpBySymbol   = await LoadLatestTicks(tickRepo);
        var chainBySymbol = await LoadOptionChainSnapshot(optionRepo);
        var latestAtrBySymbol = await LoadLatestAtrBySymbol(
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
            return true;
        }

        var unrealizedPnl = await ComputeUnrealizedPnl(paperRepo, tickRepo);
        if (realizedPnl + unrealizedPnl <= -maxDailyLoss)
        {
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

    // ── Step 3c: Latest ATR per symbol — one batched query per cycle, not per signal.
    // Reads the most recent IndicatorSnapshot row (SignalWorker's audit table) instead of
    // recalculating ATR here — ExitWorker stays execution-only. Falls back to Signal.AtrRawAtEntry
    // (in UpdateTrailingSL) when no snapshot is fresh enough (table not populated yet, or a gap).
    private static async Task<Dictionary<string, decimal>> LoadLatestAtrBySymbol(
        IRepository<IndicatorSnapshot> snapshotRepo,
        List<string> symbols)
    {
        if (symbols.Count == 0)
            return new Dictionary<string, decimal>();

        var cutoff = IstClock.Now.AddMinutes(-5);
        var recent = await snapshotRepo.FindAsync(s => symbols.Contains(s.Symbol) && s.SnapshotTime >= cutoff);

        return recent
            .GroupBy(s => s.Symbol)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.SnapshotTime).First().AtrRaw);
    }

    // ── Step 4: Evaluate single signal ───────────────────────────────────────
    private static (decimal optionLtp, decimal leg2Ltp, decimal currentDelta) ResolveLtps(
        Signal signal,
        Dictionary<string, List<OptionChainData>> chainBySymbol)
    {
        decimal optionLtp = 0;
        decimal leg2Ltp = 0;
        decimal currentDelta = 0;

        if (chainBySymbol.TryGetValue(signal.Symbol, out var chain))
        {
            var leg1 = chain.FirstOrDefault(x => x.TradingSymbol == signal.OptionSymbol);
            optionLtp = leg1?.Ltp ?? 0;
            currentDelta = leg1?.Delta ?? 0;

            if (signal.StrategyType != TradingConst.StrategyType.Single)
            {
                leg2Ltp =
                    chain.FirstOrDefault(x =>
                        x.OptionType == signal.Leg2OptionType &&
                        x.StrikePrice == signal.Leg2StrikePrice)
                    ?.Ltp ?? 0;
            }
        }
        return (optionLtp, leg2Ltp, currentDelta);
    }
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
       IRepository<Position> positionRepo,
       IRepository<SignalOutcome> outcomeRepo,
       IUnitOfWork uow,
       TradeCompletionService tradeCompletion)
    {
        if (!ltpBySymbol.TryGetValue(signal.Symbol, out var underlyingLtp))
            return;


        var (optionLtp, leg2CurrentLtp, currentDelta) =
            ResolveLtps(signal, chainBySymbol);


        if (signal.StrategyType != TradingConst.StrategyType.Single
            && leg2CurrentLtp == 0
            && !forceExit)
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
        // DetermineExitReason runs — that call can throw (e.g. from HandlePartialExit's
        // DB lookups ahead of the broker call), and this scope's DbContext is discarded on
        // the next cycle, so anything not saved yet would be silently lost rather than
        // just delayed. Order-placement saves (ClosePosition/HandlePartialExit) stay
        // immediate since those must be durable before the broker call.
        var hasChanges = false;

        // Captured BEFORE UpdateExcursion mutates signal.MfePrice — after that call, a
        // fresh high and "no change" both leave optionLtp == signal.MfePrice, so this can't
        // be recovered from the post-update state.
        var mfeIncreased = optionLtp > signal.MfePrice;

        hasChanges |= await UpdateExcursion(
            signal,
            optionLtp);


        hasChanges |= await UpdateTrailingSL(
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
        var isLiveSignal = orders.CurrentMode != TradingMode.Paper;
        if (isLiveSignal && TradingConst.IsSingleLegStrategy(signal.StrategyType))
        {
            await SyncStopLossOrderAsync(signal, orders, orderRepo, signalRepo, uow);
        }


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
        var isLive = isLiveSignal;
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
                     && signal.StopLossStatus != "PLACED"
                     && signal.LastSlUpdateTime.HasValue)
            {
                var cfg = await _settingsService.GetAsync();
                var noProtectionFor = IstClock.Now - signal.LastSlUpdateTime.Value;
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
            return null;
        }

        if (!await CancelStopLossBeforeMarketExitAsync(signal, orders, orderRepo, uow))
        {
            return null;
        }

        var exitOrder = new Order
        {
            SignalId = signal.Id,
            OrderReferenceId = Guid.NewGuid().ToString("N")[..20],
            Broker = "GROWW",
            Symbol = !string.IsNullOrWhiteSpace(signal.OptionSymbol) ? signal.OptionSymbol : signal.Symbol,
            Exchange = signal.Exchange,
            Side = "SELL",
            OrderType = "MARKET",
            OrderPurpose = MapExitReasonToPurpose(exitReason),
            ProductType = "MIS",
            Status = OrderStatus.Pending,
            Quantity = finalLots,
            OrderedAt = IstClock.Now,
            UpdatedAt = IstClock.Now,
        };
        await orderRepo.StageAsync(exitOrder);
        await uow.SaveChangesAsync();

        string closeOrderId;
        try { closeOrderId = await orders.ClosePositionAsync(signal, exitReason, finalLots, exitOrder.OrderReferenceId); }
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

        if (!await CancelStopLossBeforeMarketExitAsync(signal, orders, orderRepo, uow))
        {
            return;
        }


        var initialQty  = signal.RemainingQty > 0 ? signal.RemainingQty : signal.Quantity;
        var closeLots   = Math.Max((int)Math.Ceiling(initialQty * 0.50m), 1);
        var lotSize     = _instruments.GetLotSize(signal.Symbol);

        var partialOrder = new Order
        {
            SignalId = signal.Id,
            OrderReferenceId = Guid.NewGuid().ToString("N")[..20],
            Broker = "GROWW",
            Symbol = !string.IsNullOrWhiteSpace(signal.OptionSymbol) ? signal.OptionSymbol : signal.Symbol,
            Exchange = signal.Exchange,
            Side = "SELL",
            OrderType = "MARKET",
            OrderPurpose = Trading.Domain.OrderPurpose.PartialExit,
            ProductType = "MIS",
            Status = OrderStatus.Pending,
            Quantity = closeLots,
            OrderedAt = IstClock.Now,
            UpdatedAt = IstClock.Now,
        };
        await orderRepo.StageAsync(partialOrder);
        await uow.SaveChangesAsync();

        string partialOrderId;
        try { partialOrderId = await orders.ClosePositionAsync(signal, TradingConst.ExitReason.OptionT1Hit, closeLots, partialOrder.OrderReferenceId); }
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
    }

    // ── Broker-managed SL_M — sync (self-heal / trailing replace) ────────────────────────────
    // Called every cycle for active SINGLE/HERO_ZERO live signals, after UpdateTrailingSL may
    // have moved signal.OptionSL. Cancel-then-place, never place-then-cancel: two simultaneous
    // resting SELL orders risk a double fill, which is strictly worse than a brief protection
    // gap during the cancel→place window. Placement/cancellation both delegate to
    // StopLossOrderCoordinator, shared with BrokerOrderSyncWorker (initial placement) and
    // BrokerPositionSyncWorker (restart recovery) so the retry-cap/OrderPurpose bookkeeping
    // lives in exactly one place.
    private async Task SyncStopLossOrderAsync(
        Signal signal,
        IOrderService orders,
        IRepository<Order> orderRepo,
        IRepository<Signal> signalRepo,
        IUnitOfWork uow)
    {
        if (signal.OptionSL <= 0) return;

        var desiredQty = signal.RemainingQty > 0 ? signal.RemainingQty : signal.Quantity;

        if (string.IsNullOrEmpty(signal.StopLossOrderId))
        {
            // Self-heals a placement that failed at activation, AND handles the case right after
            // a partial exit completes elsewhere (TradeCompletionService.CompletePartialExitAsync
            // resets OptionSL to breakeven but doesn't itself talk to the broker). Internally a
            // no-op once StopLossRetryCount hits the configured cap.
            await _stopLossCoordinator.TryPlaceAsync(signal, signal.OptionSL, desiredQty, orders, orderRepo, signalRepo, uow);
            return;
        }

        // Tolerance guards float/rounding noise so a no-op cycle doesn't churn cancel/replace.
        if (Math.Abs(signal.StopLossTriggerPrice - signal.OptionSL) < 0.05m)
            return;

        if (!await _stopLossCoordinator.TryCancelAsync(signal, orders, orderRepo, uow))
            return; // already filled, or unconfirmed — coordinator logged the reason; retry next cycle

        await _stopLossCoordinator.TryPlaceAsync(signal, signal.OptionSL, desiredQty, orders, orderRepo, signalRepo, uow);
    }

    // ── Broker-managed SL_M — cancel before any MARKET sell ──────────────────────────────────
    // Called from ClosePosition/HandlePartialExit before T1/T2/ForceExit/HeroZero/ProtectedSL/
    // IndexSL fire a MARKET sell, so the resting SL_M can't also fill (double sell). MUST run
    // before the partial/full SELL is placed, never after — cancelling only after the market
    // sell would leave a window where both orders are live simultaneously. Returns false if the
    // caller must abort the MARKET exit this cycle (SL_M already filled, or its fate is
    // unconfirmed) — retried automatically next cycle since DetermineExitReason re-evaluates
    // every second.
    private Task<bool> CancelStopLossBeforeMarketExitAsync(
        Signal signal,
        IOrderService orders,
        IRepository<Order> orderRepo,
        IUnitOfWork uow)
        => _stopLossCoordinator.TryCancelAsync(signal, orders, orderRepo, uow);

    // ── Exit reason → OrderPurpose ────────────────────────────────────────────────────────────
    private static string MapExitReasonToPurpose(string exitReason) => exitReason switch
    {
        TradingConst.ExitReason.OptionT1Hit or TradingConst.ExitReason.OptionT2Hit => Trading.Domain.OrderPurpose.Target,
        TradingConst.ExitReason.ForceExitEod => Trading.Domain.OrderPurpose.ForceExit,
        TradingConst.ExitReason.OptionSlHit => Trading.Domain.OrderPurpose.StopLoss,
        _ => Trading.Domain.OrderPurpose.Exit, // HeroZero / IndexSlHit / PROTECTED_SL_HIT
    };
}
