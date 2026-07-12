using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Services;
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
        _logger.LogInformation("SignalWorker Started");

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

        var market = await BuildMarketAsync(ctx, symbol);
        if (market == null) return;

        if (!await PassPreGatesAsync(ctx, market, ct)) return;

        var strategyManager = scope.ServiceProvider.GetRequiredService<StrategyManager>();
        var signal = await strategyManager.SelectSignalAsync(ctx, market, symbol);
        if (signal is null) return;

        if (!await PassPostGatesAsync(ctx, market, signal, ct)) return;

        await PersistAndPlaceAsync(ctx, market, signal, symbol, ct);
    }

    // ── Step 1: Build market snapshot + indicator check ───────────────────────
    private async Task<MarketSnapshot?> BuildMarketAsync(SignalContext ctx, string symbol)
    {
        var market = await MarketSnapshotBuilder.BuildAsync(ctx);
        if (market == null) return null;

        // Skip EMA check for expiry day (HeroZero doesn't need EMAs)
        var skipEmaCheck = ctx.Instruments.IsExpiryDay(symbol);
        if (!skipEmaCheck && (market.Ema20 <= 0 || market.Ema50 <= 0))
        {
            _logger.LogDebug("Indicators not ready ({Symbol}) — EMA20:{E20} EMA50:{E50}",
                symbol, market.Ema20, market.Ema50);
            return null;
        }

        return market;
    }

    // ── Step 2: Pre-signal gates ──────────────────────────────────────────────
    private async Task<bool> PassPreGatesAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
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
                _logger.LogInformation("{Gate}: {Reason}", gate.GetType().Name, block);
                return false;
            }
        }
        return true;
    }

    // ── Step 4: Post-signal gates ─────────────────────────────────────────────
    private async Task<bool> PassPostGatesAsync(SignalContext ctx, MarketSnapshot market, Signal signal, CancellationToken ct)
    {
        foreach (var gate in PostGates)
        {
            var block = await gate.BlockAsync(ctx, market, signal, ct);
            if (block != null)
            {
                _logger.LogInformation("{Gate}: {Reason}", gate.GetType().Name, block);
                return false;
            }
        }
        return true;
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

        signal.Status = SignalStatus.Pending;
        signal.OrderReferenceId = Guid.NewGuid().ToString("N");
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
        catch (Exception ex)
        { _logger.LogWarning(ex, "SignalR push failed for {Symbol} signal {Id}", symbol, signal.Id); }

        _logger.LogInformation("Signal Generated {Type} {Symbol} @ {Price} Id:{Id} Order:{OrderId}",
            signal.SignalType, signal.Symbol, signal.EntryPrice, signal.Id, signal.BrokerOrderId);
    }

    // ── Step 5a: Broker order placement ──────────────────────────────────────
    private async Task PlaceOrderAsync(SignalContext ctx, Signal signal, Order order, string symbol)
    {
        string orderId = "N/A";
        try
        {
            orderId = await ctx.Orders.PlaceOrderAsync(signal);
            signal.BrokerOrderId = orderId;
            signal.Status = SignalStatus.OrderPlaced;

            // Groww accepted the order — fill status isn't known yet. Order.Status stays
            // PENDING (non-terminal) so BrokerOrderSyncWorker polls GetOrderStatusAsync
            // (now that BrokerOrderId is set) and fills in the real Status/FilledQty/AvgPrice.
            order.BrokerOrderId = orderId;
            order.UpdatedAt = IstClock.Now;

            await ctx.Uow.SaveChangesAsync();
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
            _logger.LogWarning(ex, "Order placement ambiguous for {Symbol} signal {Id} — awaiting reconciliation", symbol, signal.Id);
            await _alerts.SendCriticalAsync(
                $"Order placement UNKNOWN — {signal.SignalType} {symbol} @ ₹{signal.EntryPrice:F2}\n{ex.Message}\nWill reconcile via OrderReferenceId.");
        }
        catch (Exception ex)
        {
            // Definite rejection (GrowwOrderRejectedException) or any other unexpected failure.
            signal.Status = SignalStatus.OrderRejected;

            order.Status = OrderStatus.Failed;
            order.StatusMessage = ex.Message;
            order.UpdatedAt = IstClock.Now;

            await ctx.Uow.SaveChangesAsync();
            _logger.LogWarning(ex, "Order placement failed for {Symbol} signal {Id}", symbol, signal.Id);
            await _alerts.SendCriticalAsync(
                $"Order rejected — {signal.SignalType} {symbol} @ ₹{signal.EntryPrice:F2}\n{ex.Message}");

            if (!string.IsNullOrEmpty(orderId) && orderId != "N/A")
            {
                try { await ctx.Orders.CancelOrderAsync(orderId); }
                catch (Exception cancelEx)
                { _logger.LogWarning(cancelEx, "Failed to cancel broker order {OrderId} after rejection", orderId); }
            }
        }
    }

}
