using Trading.Application.Interfaces;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Scoring.Ranking;
using Trading.SignalEngine.Scoring.Services;
using Trading.SignalEngine.Services;
using Trading.SignalEngine.Strategies;
using Trading.Worker.Exits;
using Trading.Worker.SignalGates;
using Trading.Worker.Strategies;

namespace Trading.Worker.Extensions;

internal static class SignalEngineExtensions
{
    internal static IServiceCollection AddSignalEngine(this IServiceCollection services)
    {
        services.AddScoped<EmaService>();
        services.AddScoped<RsiService>();
        services.AddScoped<AtrService>();
        services.AddScoped<AtrRiskService>();
        services.AddScoped<PcrService>();
        services.AddScoped<MarketRegimeService>();
        services.AddScoped<MarketClassificationService>();
        services.AddScoped<OiAnalysisService>();
        services.AddScoped<TrendScoreService>();
        services.AddScoped<OiScoreService>();
        services.AddScoped<VolumeScoreService>();
        services.AddScoped<GreeksScoreService>();
        services.AddScoped<LiquidityScoreService>();
        services.AddScoped<MomentumScoreService>();
        services.AddScoped<HeroZeroScoreService>();
        services.AddScoped<StrikeRankingService>();
        services.AddScoped<StrategySelectorService>();
        services.AddScoped<SignalEngineService>();
        services.AddScoped<HeroZeroSignalEngine>();
        services.AddScoped<AdaptiveLotSizingService>();
        // Explicit factory rather than AddScoped<T>(): the evaluator has two constructors and
        // container constructor-selection would decide which one is used. Naming the dependency
        // here guarantees the settings-aware overload — and therefore the
        // EnableBankNiftyPutBreakdownRule kill switch — is actually wired in.
        services.AddScoped(sp => new SingleSignalRuleEvaluator(
            sp.GetRequiredService<IStrategySettingsService>()));
        services.AddScoped<FiveMinuteConfirmationService>();
        // BreakoutProtectionService keeps per-symbol, per-side breakout state for the whole session (day high/low,
        // breakout level, pullback) in a ConcurrentDictionary, so it MUST be a singleton: a scoped instance would be
        // rebuilt every SignalWorker cycle from only the 200-bar window. StrategyManager (scoped) requires it; without
        // this registration resolving StrategyManager throws and no signal is generated.
        services.AddSingleton<BreakoutProtectionService>();
        services.AddScoped<StrategyManager>();

        // ── Per-instrument signal generation ─────────────────────────────────
        // DefaultInstrumentStrategy is registered as its CONCRETE type only, never as
        // IInstrumentStrategy — InstrumentStrategyRegistry takes it as an explicit fallback, so a
        // real per-symbol override can never lose to it on registration order.
        // To give one instrument its own logic, add:
        //     services.AddScoped<IInstrumentStrategy, SensexInstrumentStrategy>();
        // and have it return true from Handles("SENSEX"). Every other symbol is unaffected.
        // Only do this for genuinely different decision LOGIC — threshold differences already
        // resolve per-symbol at runtime via IStrategySettingsService, with no redeploy.
        services.AddScoped<DefaultInstrumentStrategy>();
        services.AddScoped<InstrumentStrategyRegistry>();

        services.AddScoped<SignalOrderPlacementService>();
        services.AddScoped<SignalAuditService>();

        // ── Exit pipeline ────────────────────────────────────────────────────
        // ExitWorker owns the step order; each step is one of these. StopLossSyncService is the
        // only one with a hard ordering dependency (PositionCloseService must cancel a resting
        // SL_M before any MARKET sell), and that is expressed by constructor injection.
        services.AddScoped<StopLossSyncService>();
        services.AddScoped<TrailingStopService>();
        services.AddScoped<PositionCloseService>();
        services.AddScoped<ExitDecisionService>();
        services.AddScoped<HistoricalCandleService>();
        services.AddScoped<PortfolioService>();
        services.AddScoped<PerformanceAnalyticsService>();
        services.AddScoped<PaperTradingService>();
        services.AddScoped<RiskManagementService>();
        services.AddScoped<BacktestService>();
        services.AddSingleton<EventCalendarService>();
        services.AddSingleton<AppErrorLogger>();
        services.AddSingleton<SystemSettingsService>();
        services.AddScoped<StrategyPerformanceService>();
        services.AddScoped<TradeCompletionService>();
        services.AddSingleton<StopLossOrderCoordinator>();
        return services;
    }
}
