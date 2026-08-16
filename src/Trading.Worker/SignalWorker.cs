using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;
using Trading.Worker.SignalGates;

namespace Trading.Worker;

public class SignalWorker : BackgroundService
{
    private readonly ILogger<SignalWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITradingAlertService _alerts;
    private readonly IInstrumentCache _instrumentCache;
    private readonly AppErrorLogger _errorLogger;

    private static readonly IPreSignalGate[] PreGates =
    [
        new KillSwitchGate(),
        new EventBlackoutGate(),
        new StaleDataGate(),
        new MaxDailyTradesGate(),
        new CircuitBreakerGate(),
        new ConsecutiveLossGate(),
        new OnePositionGate(),
        new AtrGate(),
        new MarketRegimeGate(),
    ];

    private static readonly IPostSignalGate[] PostGates =
    [
       new OiConfirmationGate(),
        new DuplicateSignalGate(),
        new RiskGate(),
        new RiskRewardGate(),
        new SpreadGate(),
    ];

    public SignalWorker(
        ILogger<SignalWorker> logger,
        IServiceScopeFactory scopeFactory,
        ITradingAlertService alerts,
        IInstrumentCache instrumentCache,
        AppErrorLogger errorLogger)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _alerts = alerts;
        _instrumentCache = instrumentCache;
        _errorLogger = errorLogger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!MarketHours.IsSignalWindow())
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
                    continue;
                }

                var symbols = _instrumentCache.GetActive().Select(i => i.Symbol).ToList();

                foreach (var symbol in symbols)
                {
                    try { await RunForSymbol(symbol, stoppingToken); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "SignalWorker Error {Symbol}", symbol);
                        await _errorLogger.LogAsync("SignalWorker", ex, symbol);
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Signal Worker Loop Error");
                await _errorLogger.LogAsync("SignalWorker", ex, context: "main loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    private async Task RunForSymbol(string symbol, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = SignalContext.Resolve(scope.ServiceProvider, symbol);

        // Market Snapshot Audit: BuildAsync attaches an IndicatorSnapshot to market.Audit whenever
        // it successfully builds a snapshot. Kept in scope here (rather than inside a BuildMarketAsync
        // helper) so it's still reachable for the EMA-not-ready early return below, then finished and
        // saved exactly once in the finally block — this restructuring doesn't change any check itself.
        var market = await MarketSnapshotBuilder.BuildAsync(ctx);
        if (market == null) return;

        var audit = market.Audit ?? new IndicatorSnapshot { SnapshotTime = IstClock.Now, Symbol = symbol };

        try
        {
            // Skip EMA check for expiry day (HeroZero doesn't need EMAs)
            var skipEmaCheck = ctx.Instruments.IsExpiryDay(symbol);
            if (!skipEmaCheck && (market.Ema20 <= 0 || market.Ema50 <= 0))
            {
                audit.BlockedBy = "IndicatorsNotReady";
                audit.BlockedReason = $"EMA20:{market.Ema20:F2} EMA50:{market.Ema50:F2}";
                return;
            }

            if (!await PassPreGatesAsync(ctx, market, ct, audit)) return;

            var strategyManager = scope.ServiceProvider.GetRequiredService<StrategyManager>();
            var result = await strategyManager.SelectSignalAsync(ctx, market, symbol);
            audit.MarketState = result.MarketState.ToString();
            audit.StrategySelected = result.Strategy.ToString();
            audit.Confidence = result.Confidence;
            ApplyScore(audit, result.Score);

            if (result.Signal is null)
            {
                audit.SignalGenerated = false;
                audit.BlockedBy = result.BlockedBy;
                audit.BlockedReason = Truncate(result.BlockedReason, 500);
                return;
            }

            var signal = result.Signal;
            if (!await PassPostGatesAsync(ctx, market, signal, ct, audit)) return;

            audit.SignalGenerated = true;
            await PersistAndPlaceAsync(ctx, market, signal, symbol, ct);
        }
        finally
        {
            await SaveAuditAsync(scope.ServiceProvider, ctx.Uow, audit);
        }
    }

    // BlockedReason is nvarchar(500) — the combined CALL/PUT failure-condition list from
    // SignalGenerationResult can exceed that when almost every condition fails, so guard the
    // insert instead of letting SaveAuditAsync's best-effort try/catch silently swallow it.
    private static string? Truncate(string? value, int maxLength) =>
        value != null && value.Length > maxLength ? value[..maxLength] : value;

    // Score breakdown (see SignalScore) — null under the legacy AND-gated path, populated only
    // when EnableSignalScoring is on for the symbol. Kept here rather than inline for readability.
    private static void ApplyScore(IndicatorSnapshot audit, SignalScore? score)
    {
        if (score is null)
            return;

        audit.ScoreDirection = score.IsCall ? "CALL" : "PUT";
        audit.ScoreTotal = score.Total;
        audit.ScoreTrend = score.TrendScore;
        audit.ScoreEma20 = score.Ema20Score;
        audit.ScoreEma50 = score.Ema50Score;
        audit.ScoreRsi = score.RsiScore;
        audit.ScorePriceChange = score.PriceChangeScore;
        audit.ScoreCandle = score.CandleScore;
        audit.ScoreBody = score.BodyScore;
        audit.ScoreAtr = score.AtrScore;
        audit.ScorePcr = score.PcrScore;
        audit.ScoreOi = score.OiScore;
        audit.ScoreConfidence = score.ConfidenceScore;
    }

    // ── Market Snapshot Audit: best-effort, single insert, never blocks trading ───────────────
    // Failure is logged (not silently swallowed) but still never propagates — an audit-row insert
    // failure must not be able to take down signal generation.
    private async Task SaveAuditAsync(IServiceProvider sp, IUnitOfWork uow, IndicatorSnapshot audit)
    {
        try
        {
            audit.CreatedOn = IstClock.Now;
            var repo = sp.GetRequiredService<IRepository<IndicatorSnapshot>>();
            await repo.StageAsync(audit);
            await uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to save IndicatorSnapshot audit for {Symbol} at {SnapshotTime}",
                audit.Symbol, audit.SnapshotTime);
            await _errorLogger.LogAsync("SignalWorker.SaveAuditAsync", ex, audit.Symbol);
        }
    }

    // ── Step 2: Pre-signal gates ──────────────────────────────────────────────
    private async Task<bool> PassPreGatesAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct, IndicatorSnapshot audit)
    {
        var isExpiryDay = ctx.Instruments.IsExpiryDay(ctx.Symbol);

        foreach (var gate in PreGates)
        {
            // Expiry day: MarketRegimeGate not applicable — volatility is expected
            if (isExpiryDay && gate is MarketRegimeGate)
                continue;

            var block = await gate.BlockAsync(ctx, market, ct);
            if (block != null)
            {
                audit.BlockedBy = gate.GetType().Name;
                audit.BlockedReason = block;
                return false;
            }
        }
        return true;
    }

    // ── Step 4: Post-signal gates ─────────────────────────────────────────────
    private async Task<bool> PassPostGatesAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct, IndicatorSnapshot audit)
    {
        foreach (var gate in PostGates)
        {
            var block = await gate.BlockAsync(ctx, market, signal, ct);
            if (block != null)
            {
                audit.BlockedBy = gate.GetType().Name;
                audit.BlockedReason = block;
                CaptureBlockedLeg(market, signal, audit);
                return false;
            }
        }
        return true;
    }

    // Candidate option leg at post-gate block time — same lookup PersistAndPlaceAsync uses for a
    // passing signal, just recorded instead of discarded. Missed-opportunity analysis over
    // IndicatorSnapshot needs Delta/OI here since it isn't available anywhere else for blocked signals.
    private static void CaptureBlockedLeg(MarketSnapshot market, Signal signal, IndicatorSnapshot audit)
    {
        var entryLeg = market.OptionChain
            .FirstOrDefault(o => o.TradingSymbol == signal.OptionSymbol
                              || (o.StrikePrice == signal.StrikePrice && o.OptionType == signal.OptionType));

        audit.BlockedStrikePrice = signal.StrikePrice;
        audit.BlockedOptionType = signal.OptionType;
        audit.BlockedOptionSymbol = signal.OptionSymbol;
        audit.BlockedDelta = entryLeg?.Delta;
        audit.BlockedOptionLtp = entryLeg?.Ltp;
        audit.BlockedChangeInOi = entryLeg?.ChangeInOi;
    }

    // ── Step 5: Persist + place order + broadcast ─────────────────────────────
    private async Task PersistAndPlaceAsync(SignalContext ctx, MarketSnapshot market, Signal signal, string symbol, CancellationToken ct)
    {
        var maxLots = ctx.Settings.MaxLotSize;
        if (maxLots > 0 && signal.Quantity > maxLots)
            signal.Quantity = maxLots;

        // Capture market context at entry for journal analytics
        signal.RsiAtEntry   = market.Rsi;
        signal.PcrAtEntry   = market.PcrValue;

        var entryLeg = market.OptionChain
            .FirstOrDefault(o => o.TradingSymbol == signal.OptionSymbol
                              || (o.StrikePrice == signal.StrikePrice && o.OptionType == signal.OptionType));
        signal.DeltaAtEntry      = entryLeg?.Delta      ?? 0m;
        signal.ChangeInOiAtEntry = entryLeg?.ChangeInOi ?? 0m;
        signal.AtrPctAtEntry     = market.AtrPct;
        signal.AtrRawAtEntry     = market.AtrRaw;

        signal.Status = SignalStatus.Pending;
       // signal.OrderReferenceId = Guid.NewGuid().ToString("N");
        signal.OrderReferenceId = Guid.NewGuid().ToString("N")[..20];
        await ctx.SignalRepo.StageAsync(signal);
        await ctx.Uow.SaveChangesAsync();

        // Order row inserted and saved BEFORE the broker call — if the process crashes or
        // the call times out between here and Groww's response, the order is still visible
        // (Status=PENDING) instead of lost. BrokerOrderSyncWorker reconciles it later via
        // OrderReferenceId.
        var order = new Order
        {
            SignalId = signal.Id,
            OrderReferenceId = signal.OrderReferenceId,
            Broker = "GROWW",
            Symbol = !string.IsNullOrWhiteSpace(signal.OptionSymbol) ? signal.OptionSymbol : signal.Symbol,
            Exchange = signal.Exchange,
            Side = "BUY",
            OrderType = "LIMIT",
            OrderPurpose = Trading.Domain.OrderPurpose.Entry,
            ProductType = "MIS",
            Status = OrderStatus.Pending,
            Price = signal.OptionLtp > 0 ? signal.OptionLtp : signal.EntryPrice,
            Quantity = signal.Quantity,
            OrderedAt = IstClock.Now,
            UpdatedAt = IstClock.Now,
        };
        await ctx.OrderRepo.StageAsync(order);
        await ctx.Uow.SaveChangesAsync();

        await PlaceOrderAsync(ctx, signal, order, symbol);

        try { await ctx.Hub.SendSignalAsync(signal); }
        catch (Exception) { }
    }

    // ── Step 5a: Broker order placement ──────────────────────────────────────
    private async Task PlaceOrderAsync(SignalContext ctx, Signal signal, Order order, string symbol)
    {
        string orderId;
        try
        {
            orderId = await ctx.Orders.PlaceOrderAsync(signal);
        }
        catch (GrowwOrderAmbiguousException ex)
        {
            // Timeout/network/5xx — Groww may have processed this anyway. Do NOT mark
            // rejected: leave Order=ORDER_UNKNOWN, Signal=PENDING for BrokerOrderSyncWorker
            // to reconcile via OrderReferenceId.
            order.Status = OrderStatus.OrderUnknown;
            order.StatusMessage = ex.Message;
            order.UpdatedAt = IstClock.Now;
            await ctx.Uow.SaveChangesAsync();
            await _alerts.SendCriticalAsync(
                $"Order placement UNKNOWN — {signal.SignalType} {symbol} @ ₹{signal.EntryPrice:F2}\n{ex.Message}\nWill reconcile via OrderReferenceId.");
            return;
        }
        catch (Exception ex)
        {
            // Broker call itself failed — no orderId was ever obtained. Genuine rejection.
            signal.Status = SignalStatus.OrderRejected;

            order.Status = OrderStatus.Failed;
            order.StatusMessage = ex.Message;
            order.UpdatedAt = IstClock.Now;

            await ctx.Uow.SaveChangesAsync();
            await _alerts.SendCriticalAsync(
                $"Order rejected — {signal.SignalType} {symbol} @ ₹{signal.EntryPrice:F2}\n{ex.Message}");
            return;
        }

        // Groww confirmed the order — from here on any failure is OUR failure, not a broker
        // rejection. Never mark OrderRejected or cancel once we hold a real BrokerOrderId.
        signal.BrokerOrderId = orderId;
        signal.Status = SignalStatus.OrderPlaced;

        // Fill status isn't known yet. Order.Status stays PENDING (non-terminal) so
        // BrokerOrderSyncWorker polls GetOrderStatusAsync (now that BrokerOrderId is set)
        // and fills in the real Status/FilledQty/AvgPrice.
        order.BrokerOrderId = orderId;
        order.UpdatedAt = IstClock.Now;

        try
        {
            await ctx.Uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // DB save failed AFTER the broker accepted the order — the position is real.
            // Leave BrokerOrderId set and defer to BrokerOrderSyncWorker for reconciliation
            // instead of mislabeling this as a rejection.
            order.Status = OrderStatus.OrderUnknown;
            order.StatusMessage = ex.Message;
            await _alerts.SendCriticalAsync(
                $"Order placed at broker (OrderId={orderId}) but local save FAILED — {signal.SignalType} {symbol} @ ₹{signal.EntryPrice:F2}\n{ex.Message}\nWill reconcile via BrokerOrderId.");
        }
    }

}
