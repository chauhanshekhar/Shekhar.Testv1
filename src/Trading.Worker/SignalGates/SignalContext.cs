using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;

namespace Trading.Worker.SignalGates;

public record SignalContext(
    string Symbol,
    IRepository<Candle> CandleRepo,
    IRepository<Signal> SignalRepo,
    IRepository<Order> OrderRepo,
    IRepository<OptionChainData> OptionRepo,
    IRepository<SystemSetting> SettingsRepo,
    IRepository<DailyPnL> PnlRepo,
    IRepository<TradeJournal> JournalRepo,
    IRepository<EconomicEvent> EventRepo,
    IRepository<MarketTick> TickRepo,
    EmaService Ema,
    RsiService Rsi,
    AtrService Atr,
    PcrService Pcr,
    OiAnalysisService Oi,
    MarketRegimeService Regime,
    SignalEngineService Engine,
    HeroZeroSignalEngine HeroZeroEngine,
    RiskManagementService Risk,
    IOrderService Orders,
    IGrowwLiveDataService LiveData,
    INotificationService Hub,
    IUnitOfWork Uow,
    TradingSettings Settings,
    EventCalendarService EventCalendar,
    IInstrumentCache Instruments,
    SystemSettingsService SystemSettings,
    IRuntimeSettingsService RuntimeSettings,
    IStrategySettingsService StrategySettings,
    IServiceProvider? ServiceProvider = null)
{
    /// <summary>
    /// Resolve is synchronous for BackgroundService compatibility.
    /// Settings are loaded from DB via SystemSettingsService (cached 60s).
    /// Use ResolveAsync when you need guaranteed-fresh DB settings.
    /// </summary>
    public static SignalContext Resolve(IServiceProvider sp, string symbol)
    {
        var svc      = sp.GetRequiredService<SystemSettingsService>();
        var settings = svc.GetAsync().GetAwaiter().GetResult();   // cached — not a real sync-over-async
        return Build(sp, symbol, svc, settings);
    }

    public static async Task<SignalContext> ResolveAsync(IServiceProvider sp, string symbol, CancellationToken ct = default)
    {
        var svc      = sp.GetRequiredService<SystemSettingsService>();
        var settings = await svc.GetAsync(ct);
        return Build(sp, symbol, svc, settings);
    }

    private static SignalContext Build(IServiceProvider sp, string symbol, SystemSettingsService svc, TradingSettings settings) =>
        new(
            Symbol: symbol,
            CandleRepo: sp.GetRequiredService<IRepository<Candle>>(),
            SignalRepo: sp.GetRequiredService<IRepository<Signal>>(),
            OrderRepo: sp.GetRequiredService<IRepository<Order>>(),
            OptionRepo: sp.GetRequiredService<IRepository<OptionChainData>>(),
            SettingsRepo: sp.GetRequiredService<IRepository<SystemSetting>>(),
            PnlRepo: sp.GetRequiredService<IRepository<DailyPnL>>(),
            JournalRepo: sp.GetRequiredService<IRepository<TradeJournal>>(),
            EventRepo: sp.GetRequiredService<IRepository<EconomicEvent>>(),
            TickRepo: sp.GetRequiredService<IRepository<MarketTick>>(),
            Ema: sp.GetRequiredService<EmaService>(),
            Rsi: sp.GetRequiredService<RsiService>(),
            Atr: sp.GetRequiredService<AtrService>(),
            Pcr: sp.GetRequiredService<PcrService>(),
            Oi: sp.GetRequiredService<OiAnalysisService>(),
            Regime: sp.GetRequiredService<MarketRegimeService>(),
            Engine: sp.GetRequiredService<SignalEngineService>(),
            HeroZeroEngine: sp.GetRequiredService<HeroZeroSignalEngine>(),
            Risk: sp.GetRequiredService<RiskManagementService>(),
            Orders: sp.GetRequiredService<IOrderService>(),
            LiveData: sp.GetRequiredService<IGrowwLiveDataService>(),
            Hub: sp.GetRequiredService<INotificationService>(),
            Uow: sp.GetRequiredService<IUnitOfWork>(),
            Settings: settings,
            EventCalendar: sp.GetRequiredService<EventCalendarService>(),
            Instruments: sp.GetRequiredService<IInstrumentCache>(),
            SystemSettings: svc,
            RuntimeSettings: sp.GetRequiredService<IRuntimeSettingsService>(),
            StrategySettings: sp.GetRequiredService<IStrategySettingsService>(),
            ServiceProvider: sp);
}
