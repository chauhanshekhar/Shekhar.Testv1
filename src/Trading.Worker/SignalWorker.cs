using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Services;
using Trading.Worker.SignalGates;
using Trading.Worker.Strategies;

namespace Trading.Worker;

/// <summary>
/// Signal pipeline sequence for every active instrument, once per second inside the signal
/// window. This worker owns the ORDER of the steps and nothing else — each step lives in its own
/// service:
///
///   1. MarketSnapshotBuilder       — indicators, regime, option chain, audit row
///   2. PreGates                    — kill switch / stale data / ATR / regime / risk counters
///   3. InstrumentStrategyRegistry  — resolves the strategy owning this symbol, which returns the
///                                    Signal (DefaultInstrumentStrategy -> StrategyManager for all
///                                    symbols unless one is explicitly overridden)
///   4. PostGates                   — OI confirmation / duplicate / risk / R:R / spread
///   5. SignalOrderPlacementService — persist, place at broker, broadcast
///
/// The audit row is written exactly once per cycle in the finally block, whichever step ended it.
/// </summary>
public class SignalWorker : BackgroundService
{
    private readonly ILogger<SignalWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInstrumentCache _instrumentCache;
    private readonly AppErrorLogger _errorLogger;

    private static readonly IPreSignalGate[] PreGates =
    [
        new KillSwitchGate(),
        new EventBlackoutGate(),
        new StaleDataGate(),
         new ConsecutiveLossGate(),
        new MaxDailyTradesGate(),      // EnableMaxDailyTradesGate + MaxTradesPerDay; same position as the backtest (after ConsecutiveLossGate, before OnePositionGate)
        new OnePositionGate(),
        new AtrGate(),
        new MarketRegimeGate(),
    ];

    internal static readonly IPostSignalGate[] PostGates =
    [
    //new WhipsawShadowGate(),       // First: diagnostic only, never blocks
    new OiConfirmationGate(),
    new DuplicateSignalGate(),
    new RiskGate(),
    new IndexStopFloorGate(),
    new StaleEntryGate(),
    new EarlyTrendGate(),
    new NoTradeWindowGate(),       // EnableNoTradeWindowGate; same position as the backtest (after EarlyTrendGate)
    new OpeningWarmupGate(),       // EnableOpeningWarmupGate (default off) - no entries before 09:45 while 1m EMAs warm up
    new Trend5mGate(),             // EnableTrend5mGate + Trend5mGateSide + Trend5mRequirePriceSide - 5m EMA20 vs EMA50 must agree with the side
    new CloseBreakoutGate(),       // EnableCloseBreakoutGate (default off; per symbol) + CloseBreakoutGateSide (0 both, 1 CALL, 2 PUT)
    new EfficiencyRatioGate(),     // EnableEfficiencyRatioGate (default off) - blocks choppy 5m markets (same rule the backtest applies)
    new StopNoiseGate(),           // EnableStopNoiseGate (default off) - blocks stops smaller than the average 5m bar range
    new ChopGate(),
    new FastEmaGate(),
    new SessionTrendGate(),
    new CrossIndexAgreementGate(),
    new MarketDirectionGate(),
    //new VwapSideGate(),
    new HandoffCooldownGate(),
    new RiskRewardGate(),
    new SpreadGate(),
    ];

    public SignalWorker(
        ILogger<SignalWorker> logger,
        IServiceScopeFactory scopeFactory,
        IInstrumentCache instrumentCache,
        AppErrorLogger errorLogger)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
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
        var auditService = scope.ServiceProvider.GetRequiredService<SignalAuditService>();

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

            // Change 9: opening-gap observability. Logs only when the regime actually flags a gap,
            // so it cannot flood production on ordinary days. Emitted for both outcomes so a
            // suppressed gap morning is as visible as a traded one -- the previous behaviour
            // (regime forced to Sideways/Low before ~09:35) was invisible in the logs.
            var gapFlagged = market.MarketRegime.IsGapUp || market.MarketRegime.IsGapDown;

            if (!await PassPreGatesAsync(ctx, market, ct, audit))
            {
                return;
            }



            // ── Step 3: per-instrument signal generation ──────────────────────
            // Resolves to DefaultInstrumentStrategy (-> StrategyManager) for every symbol with no
            // dedicated override registered, which today is all of them.
            var strategy = scope.ServiceProvider
                .GetRequiredService<InstrumentStrategyRegistry>()
                .For(symbol);

            var result = await strategy.GenerateAsync(ctx, market, symbol, ct);
            audit.MarketState = result.MarketState.ToString();
            audit.StrategySelected = result.Strategy.ToString();
            audit.Confidence = result.Confidence;

            if (result.Signal is null)
            {
                audit.SignalGenerated = false;
                audit.BlockedBy = result.BlockedBy;
                audit.BlockedReason = SignalAuditService.Truncate(result.BlockedReason, 500);
                return;
            }

            var signal = result.Signal;
            if (!await PassPostGatesAsync(ctx, market, signal, ct, audit)) return;

            audit.SignalGenerated = true;
            await scope.ServiceProvider
                .GetRequiredService<SignalOrderPlacementService>()
                .PersistAndPlaceAsync(ctx, market, signal, symbol, ct);
        }
        finally
        {
            await auditService.SaveAsync(scope.ServiceProvider, ctx.Uow, audit);
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
                SignalAuditService.CaptureBlockedLeg(market, signal, audit);
                return false;
            }
        }
        return true;
    }
}
