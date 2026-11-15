using Microsoft.EntityFrameworkCore;
using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Infrastructure.Data;

public class TradingDbContext : DbContext
{
    public TradingDbContext(DbContextOptions<TradingDbContext> options) : base(options) { }
    public DbSet<BacktestResult> BacktestResult => Set<BacktestResult>();
    public DbSet<Candle> Candles => Set<Candle>();
    public DbSet<DailyPnL> DailyPnL => Set<DailyPnL>();
    public DbSet<MarketTick> MarketTicks => Set<MarketTick>();
    public DbSet<OptionChainData> OptionChainData => Set<OptionChainData>();
    public DbSet<OptionMinuteHistory> OptionMinuteHistory => Set<OptionMinuteHistory>();
    public DbSet<PaperTrade> PaperTrades => Set<PaperTrade>();
    public DbSet<Portfolio> Portfolios => Set<Portfolio>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Signal> Signals => Set<Signal>();
    public DbSet<SymbolIndicator> SymbolIndicators => Set<SymbolIndicator>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<TradeJournal> TradeJournal => Set<TradeJournal>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<User> Users => Set<User>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
    public DbSet<EconomicEvent> EconomicEvents => Set<EconomicEvent>();
    public DbSet<TradingInstrument> TradingInstruments => Set<TradingInstrument>();
    public DbSet<AppError> AppErrors => Set<AppError>();
    public DbSet<SignalOutcome> SignalOutcomes => Set<SignalOutcome>();
    public DbSet<ExchangeHoliday> ExchangeHolidays => Set<ExchangeHoliday>();
    public DbSet<HeroZeroScore> HeroZeroScores => Set<HeroZeroScore>();
    public DbSet<StrategyExecutionLog> StrategyExecutionLogs => Set<StrategyExecutionLog>();
    public DbSet<StrategyPerformance> StrategyPerformances => Set<StrategyPerformance>();
    public DbSet<IndicatorSnapshot> IndicatorSnapshots => Set<IndicatorSnapshot>();
    public DbSet<TrailingStopLog> TrailingStopLogs => Set<TrailingStopLog>();
    public DbSet<StrategySetting> StrategySettings => Set<StrategySetting>();
    public DbSet<IndicatorSnapshotOutcome> IndicatorSnapshotOutcomes => Set<IndicatorSnapshotOutcome>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<User>(e =>
        {
            e.Property(u => u.FullName).HasMaxLength(100).IsRequired();
            e.Property(u => u.Email).HasMaxLength(100).IsRequired();
            e.Property(u => u.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(u => u.Role).HasMaxLength(20).HasDefaultValue("Trader");
            e.HasIndex(u => u.Email).IsUnique();
        });

        mb.Entity<Signal>(e =>
        {
            e.Property(s => s.Rsi5mAtEntry).HasPrecision(18, 4);
            e.Property(s => s.Symbol).HasMaxLength(50).IsRequired();
            e.Property(s => s.SignalType).HasMaxLength(10).IsRequired();
            e.Property(s => s.EntryPrice).HasPrecision(18, 2);
            e.Property(s => s.StopLoss).HasPrecision(18, 2);
            e.Property(s => s.Target1).HasPrecision(18, 2);
            e.Property(s => s.Target2).HasPrecision(18, 2);
            e.Property(s => s.Confidence).HasPrecision(5, 2);
            e.Property(s => s.Exchange).HasMaxLength(5).HasDefaultValue("NSE");
            e.Property(s => s.OptionSymbol).HasMaxLength(50);

            // ExitWorker: WHERE CreatedOn >= todayUtc
            // SignalWorker duplicate check: WHERE Symbol=? AND CreatedOn >= fiveMinutesAgo
            e.HasIndex(s => new { s.Symbol, s.CreatedOn })
             .HasDatabaseName("IX_Signal_Symbol_CreatedOn");

            e.Property(s => s.StrategyType).HasMaxLength(20).HasDefaultValue("SINGLE");
            e.Property(s => s.SelectedStrategy).HasMaxLength(20).HasDefaultValue("");
            e.Property(s => s.MarketState).HasMaxLength(30).HasDefaultValue("");
            e.Property(s => s.Leg2Symbol).HasMaxLength(50);
            e.Property(s => s.Leg2OrderId).HasMaxLength(50);
            e.Property(s => s.Leg2Ltp).HasPrecision(18, 2);
            e.Property(s => s.Leg2StrikePrice).HasPrecision(18, 2);
            e.Property(s => s.MfePrice).HasPrecision(18, 2);
            e.Property(s => s.MaePrice).HasPrecision(18, 2);
            e.Property(s => s.Status).HasMaxLength(20).HasDefaultValue(SignalStatus.Pending);
            e.HasIndex(s => s.Status).HasDatabaseName("IX_Signal_Status");
            e.Property(s => s.OrderReferenceId).HasMaxLength(50);
            e.Property(s => s.BrokerOrderStatus).HasMaxLength(30);
            e.Property(s => s.AtrPctAtEntry).HasPrecision(9, 4);
            e.Property(s => s.AtrRawAtEntry).HasPrecision(18, 4);
            e.Property(s => s.StopLossOrderId).HasMaxLength(50).HasDefaultValue("");
            e.Property(s => s.StopLossStatus).HasMaxLength(20).HasDefaultValue("");
            e.Property(s => s.StopLossTriggerPrice).HasPrecision(18, 2);
        });

        mb.Entity<SignalOutcome>(e =>
        {
            e.Property(o => o.Symbol).HasMaxLength(50).IsRequired();
            e.Property(o => o.StrategyType).HasMaxLength(20);
            e.Property(o => o.Result).HasMaxLength(10);
            e.Property(o => o.ExitReason).HasMaxLength(50);
            e.Property(o => o.EntryPrice).HasPrecision(18, 2);
            e.Property(o => o.ExitPrice).HasPrecision(18, 2);
            e.Property(o => o.PnL).HasPrecision(18, 2);
            e.Property(o => o.Mfe).HasPrecision(18, 2);
            e.Property(o => o.Mae).HasPrecision(18, 2);
            e.Property(o => o.MfePct).HasPrecision(10, 4);
            e.Property(o => o.MaePct).HasPrecision(10, 4);
            e.Property(o => o.Confidence).HasPrecision(5, 2);
            // Fast analytics queries: per-symbol win-rate, per-strategy PF
            e.HasIndex(o => new { o.Symbol, o.CreatedOn })
             .HasDatabaseName("IX_SignalOutcome_Symbol_CreatedOn");
            e.HasIndex(o => o.SignalId).IsUnique()
             .HasDatabaseName("IX_SignalOutcome_SignalId");
        });

        mb.Entity<MarketTick>(e =>
        {
            e.Property(t => t.Symbol).HasMaxLength(50).IsRequired();
            e.Property(t => t.LastPrice).HasPrecision(18, 2);

            // ExitWorker: WHERE TickTime >= cutoff (last 30s)
            e.HasIndex(t => new { t.Symbol, t.TickTime })
             .HasDatabaseName("IX_MarketTick_Symbol_TickTime");
        });

        mb.Entity<Candle>(e =>
        {
            e.Property(c => c.Symbol).HasMaxLength(50).IsRequired();
            e.Property(c => c.Open).HasPrecision(18, 2);
            e.Property(c => c.High).HasPrecision(18, 2);
            e.Property(c => c.Low).HasPrecision(18, 2);
            e.Property(c => c.Close).HasPrecision(18, 2);

            // SignalWorker: WHERE Symbol=? AND CandleTime >= dayStartUtc.AddDays(-1)
            e.HasIndex(c => new { c.Symbol, c.CandleTime })
             .HasDatabaseName("IX_Candle_Symbol_CandleTime");
        });

        mb.Entity<Position>(e =>
        {
            e.Property(p => p.Symbol).HasMaxLength(50).IsRequired();
            e.Property(p => p.AvgPrice).HasPrecision(18, 2);
            e.Property(p => p.CurrentPrice).HasPrecision(18, 2);
            e.Property(p => p.PnL).HasPrecision(18, 2);
            e.Property(p => p.OptionSymbol).HasMaxLength(50);
            e.Property(p => p.Status).HasMaxLength(20).HasDefaultValue("OPEN");
            e.Property(p => p.PositionSource).HasMaxLength(20).HasDefaultValue("BROKER_SYNC");
            e.HasIndex(p => p.SignalId).HasDatabaseName("IX_Position_SignalId");
            e.HasIndex(p => p.PositionSource).HasDatabaseName("IX_Position_PositionSource");
        });

        mb.Entity<Portfolio>(e =>
        {
            e.Property(p => p.Capital).HasPrecision(18, 2);
            e.Property(p => p.UsedMargin).HasPrecision(18, 2);
            e.Property(p => p.AvailableMargin).HasPrecision(18, 2);
            e.Property(p => p.NetPnL).HasPrecision(18, 2);
        });

        mb.Entity<OptionChainData>(e =>
        {
            e.Property(o => o.Symbol).HasMaxLength(50).IsRequired();
            e.Property(o => o.TradingSymbol).HasMaxLength(50);
            e.Property(o => o.Expiry).HasMaxLength(20);
            e.Property(o => o.SnapshotId).HasMaxLength(36);
            e.Property(o => o.StrikePrice).HasPrecision(18, 2);
            e.Property(o => o.Ltp).HasPrecision(18, 2);
            e.Property(o => o.UnderlyingLtp).HasPrecision(18, 2);
            e.Property(o => o.ImpliedVolatility).HasPrecision(18, 4);
            e.Property(o => o.Delta).HasPrecision(10, 6);
            e.Property(o => o.Gamma).HasPrecision(10, 6);
            e.Property(o => o.Theta).HasPrecision(10, 6);
            e.Property(o => o.Vega).HasPrecision(10, 6);
            e.Property(o => o.Rho).HasPrecision(10, 6);

            // Fast lookup/delete of all rows for the current snapshot
            e.HasIndex(o => new { o.Symbol, o.Expiry })
             .HasDatabaseName("IX_OptionChainData_Symbol_Expiry");

            // Fast prune query (WHERE Symbol=? AND CreatedOn < cutoff)
            e.HasIndex(o => new { o.Symbol, o.CreatedOn })
             .HasDatabaseName("IX_OptionChainData_Symbol_CreatedOn");

            // Fast expiry-range queries and analytics
            e.HasIndex(o => new { o.Symbol, o.ExpiryDate })
             .HasDatabaseName("IX_OptionChainData_Symbol_ExpiryDate");
        });

        // Per-minute copy of the ATM±5 chain for backtesting (see OptionMinuteHistory). Analysis only.
        mb.Entity<OptionMinuteHistory>(e =>
        {
            e.ToTable("OptionMinuteHistory");
            e.Property(o => o.Symbol).HasMaxLength(20).IsRequired();
            e.Property(o => o.TradingSymbol).HasMaxLength(50).IsRequired();
            e.Property(o => o.OptionType).HasMaxLength(2).IsRequired();
            e.Property(o => o.StrikePrice).HasPrecision(18, 2);
            e.Property(o => o.Ltp).HasPrecision(18, 2);
            e.Property(o => o.UnderlyingLtp).HasPrecision(18, 2);
            e.Property(o => o.ImpliedVolatility).HasPrecision(18, 4);
            e.Property(o => o.Delta).HasPrecision(10, 6);

            // One row per leg per minute: makes the per-minute write idempotent, and serves the
            // backtest's read pattern (WHERE Symbol=? AND MinuteTime BETWEEN ? AND ?).
            e.HasIndex(o => new { o.Symbol, o.MinuteTime, o.TradingSymbol })
             .IsUnique()
             .HasDatabaseName("UX_OptionMinuteHistory_Symbol_Minute_Leg");

            // Retention delete (WHERE MinuteTime < cutoff)
            e.HasIndex(o => o.MinuteTime)
             .HasDatabaseName("IX_OptionMinuteHistory_MinuteTime");
        });

        mb.Entity<DailyPnL>(e =>
        {
            e.Property(d => d.TotalProfit).HasPrecision(18, 2);
            e.Property(d => d.TotalLoss).HasPrecision(18, 2);
            e.Property(d => d.NetProfit).HasPrecision(18, 2);
            e.Property(d => d.WinRate).HasPrecision(5, 2);
            e.Property(d => d.ProfitFactor).HasPrecision(10, 4);
            e.Property(d => d.MaxDrawdown).HasPrecision(18, 2);
            e.Property(d => d.RealizedPnl).HasPrecision(18, 2);
            e.Property(d => d.UnrealizedPnl).HasPrecision(18, 2);
            e.Property(d => d.DailyDrawdown).HasPrecision(18, 2);
            // One row per trading day — fast look-up by date
            e.HasIndex(d => d.TradeDate).IsUnique()
             .HasDatabaseName("IX_DailyPnL_TradeDate");
        });

        mb.Entity<TradeJournal>(e =>
        {
            e.Property(j => j.Rsi5mAtEntry).HasPrecision(18, 4);
            e.Property(t => t.Symbol).HasMaxLength(50).IsRequired();
            e.Property(t => t.EntryPrice).HasPrecision(18, 2);
            e.Property(t => t.ExitPrice).HasPrecision(18, 2);
            e.Property(t => t.ProfitLoss).HasPrecision(18, 2);

            // DailyPnlWorker: WHERE TradeDate >= todayStart AND TradeDate < todayEnd
            // ExitWorker: WHERE TradeDate >= todayUtc
            e.HasIndex(t => t.TradeDate)
             .HasDatabaseName("IX_TradeJournal_TradeDate");
            e.Property(t => t.ExitReason).HasMaxLength(50);
            e.Property(t => t.SelectedStrategy).HasMaxLength(20).HasDefaultValue("");
            e.Property(t => t.MarketState).HasMaxLength(30).HasDefaultValue("");
        });

        mb.Entity<PaperTrade>(e =>
        {
            e.Property(p => p.Symbol).HasMaxLength(50).IsRequired();
            e.Property(p => p.OptionSymbol).HasMaxLength(50);
            e.Property(p => p.Side).HasMaxLength(10);
            e.Property(p => p.Status).HasMaxLength(20);
            e.Property(p => p.CloseReason).HasMaxLength(50);
            e.Property(p => p.EntryPrice).HasPrecision(18, 2);
            e.Property(p => p.StopLoss).HasPrecision(18, 2);
            e.Property(p => p.Target1).HasPrecision(18, 2);
            e.Property(p => p.Target2).HasPrecision(18, 2);
            e.Property(p => p.ExitPrice).HasPrecision(18, 2);
            e.Property(p => p.ProfitLoss).HasPrecision(18, 2);
            // Fast lookup of open trades by symbol
            e.HasIndex(p => new { p.Symbol, p.Status })
             .HasDatabaseName("IX_PaperTrade_Symbol_Status");
        });

        mb.Entity<Order>(e =>
        {
            e.Property(o => o.BrokerOrderId).HasMaxLength(50).IsRequired();
            e.Property(o => o.OrderReferenceId).HasMaxLength(50);
            e.Property(o => o.Broker).HasMaxLength(20).HasDefaultValue("GROWW");
            e.Property(o => o.Symbol).HasMaxLength(50).IsRequired();
            e.Property(o => o.Exchange).HasMaxLength(10);
            e.Property(o => o.Side).HasMaxLength(10);
            e.Property(o => o.OrderType).HasMaxLength(20);
            e.Property(o => o.OrderPurpose).HasMaxLength(20).HasDefaultValue("");
            e.Property(o => o.ProductType).HasMaxLength(20);
            e.Property(o => o.Status).HasMaxLength(30);
            e.Property(o => o.StatusMessage).HasMaxLength(500);
            e.Property(o => o.Price).HasPrecision(18, 2);
            e.Property(o => o.TriggerPrice).HasPrecision(18, 2);
            e.Property(o => o.AvgPrice).HasPrecision(18, 2);
            // Filtered: BrokerOrderId is "" for every order still PENDING/ORDER_UNKNOWN before
            // Groww confirms it — a plain unique index would collide across those rows.
            e.HasIndex(o => o.BrokerOrderId).IsUnique()
             .HasFilter("[BrokerOrderId] <> ''")
             .HasDatabaseName("IX_Order_BrokerOrderId");
            e.HasIndex(o => o.Status)
             .HasDatabaseName("IX_Order_Status");
            e.HasIndex(o => o.SignalId)
             .HasDatabaseName("IX_Order_SignalId");
            e.HasIndex(o => o.OrderReferenceId).IsUnique()
             .HasFilter("[OrderReferenceId] <> ''")
             .HasDatabaseName("IX_Order_OrderReferenceId");
        });

        mb.Entity<SystemSetting>(e =>
        {
            e.Property(s => s.Key).HasMaxLength(100).IsRequired();
            e.Property(s => s.Value).HasColumnType("nvarchar(max)");
            e.HasIndex(s => s.Key).IsUnique();
        });

        mb.Entity<SymbolIndicator>(e =>
        {
            e.Property(i => i.Symbol).HasMaxLength(50).IsRequired();
            e.Property(i => i.IndicatorName).HasMaxLength(20).IsRequired();
            e.Property(i => i.Value).HasPrecision(18, 4);
            // One row per symbol+indicator — upsert pattern keeps table tiny
            e.HasIndex(i => new { i.Symbol, i.IndicatorName }).IsUnique();
        });

        mb.Entity<SubscriptionPlan>(e =>
        {
            e.Property(p => p.Name).HasMaxLength(100).IsRequired();
            e.Property(p => p.Price).HasPrecision(18, 2);
            e.Property(p => p.Features).HasMaxLength(2000);
        });

        mb.Entity<UserSubscription>(e =>
        {
            e.Property(u => u.PlanName).HasMaxLength(100);
            e.HasIndex(u => new { u.UserId, u.IsActive });
        });

        mb.Entity<EconomicEvent>(e =>
        {
            e.Property(ev => ev.Name).HasMaxLength(200).IsRequired();
            e.Property(ev => ev.Impact).HasMaxLength(10).HasDefaultValue("HIGH");
            e.Property(ev => ev.NewsEntryThresholdPct).HasPrecision(5, 2);
            e.HasIndex(ev => ev.EventTimeUtc).HasDatabaseName("IX_EconomicEvent_EventTimeUtc");
        });

        mb.Entity<TradingInstrument>(e =>
        {
            e.Property(t => t.Symbol).HasMaxLength(20).IsRequired();
            e.Property(t => t.Exchange).HasMaxLength(5).HasDefaultValue("NSE");
            e.Property(t => t.Expiry).HasMaxLength(20);
            e.HasIndex(t => t.Symbol).IsUnique()
             .HasDatabaseName("IX_TradingInstrument_Symbol");
        });

        mb.Entity<AppError>(e =>
        {
            mb.Entity<AppError>(e =>
            {
                e.Property(a => a.Source)
                    .HasMaxLength(250)
                    .IsRequired();


                e.Property(a => a.Message)
                    .HasColumnType("nvarchar(max)")
                    .IsRequired();


                e.Property(a => a.StackTrace)
                    .HasColumnType("nvarchar(max)");


                e.Property(a => a.InnerMessage)
                    .HasColumnType("nvarchar(max)");


                e.Property(a => a.Context)
                    .HasColumnType("nvarchar(max)");


                e.Property(a => a.Symbol)
                    .HasMaxLength(50);


                e.Property(a => a.Severity)
                    .HasMaxLength(20)
                    .HasDefaultValue("ERROR");


                e.HasIndex(a => new
                {
                    a.Source,
                    a.OccurredOn
                })
                .HasDatabaseName(
                    "IX_AppError_Source_OccurredOn");


                e.HasIndex(a => a.OccurredOn)
                .HasDatabaseName(
                    "IX_AppError_OccurredOn");
            });
        });
        mb.Entity<ExchangeHoliday>(e =>
        {
            e.Property(h => h.Description).HasMaxLength(200);
            e.HasIndex(h => h.HolidayDate).IsUnique()
             .HasDatabaseName("IX_ExchangeHoliday_Date");
        });

        mb.Entity<HeroZeroScore>(e =>
        {
            e.Property(s => s.Symbol).HasMaxLength(50).IsRequired();
            e.Property(s => s.TradingSymbol).HasMaxLength(50);
            e.Property(s => s.OptionType).HasMaxLength(5).IsRequired();
            e.Property(s => s.StrikePrice).HasPrecision(18, 2);
            e.Property(s => s.TrendScore).HasPrecision(18, 2);
            e.Property(s => s.OiScore).HasPrecision(18, 2);
            e.Property(s => s.VolumeScore).HasPrecision(18, 2);
            e.Property(s => s.GreeksScore).HasPrecision(18, 2);
            e.Property(s => s.LiquidityScore).HasPrecision(18, 2);
            e.Property(s => s.MomentumScore).HasPrecision(18, 2);
            e.Property(s => s.TotalScore).HasPrecision(18, 2);

            // Backtesting/analytics: WHERE Symbol=? AND CreatedOn BETWEEN ...
            e.HasIndex(s => new { s.Symbol, s.CreatedOn })
             .HasDatabaseName("IX_HeroZeroScore_Symbol_CreatedOn");
        });

        mb.Entity<StrategyExecutionLog>(e =>
        {
            e.Property(s => s.Symbol).HasMaxLength(50).IsRequired();
            e.Property(s => s.SelectedStrategy).HasMaxLength(20).IsRequired();
            e.Property(s => s.MarketState).HasMaxLength(30).IsRequired();
            e.Property(s => s.Reason).HasMaxLength(500);
            e.Property(s => s.Score).HasPrecision(18, 2);

            // Audit/analytics: WHERE Symbol=? AND CreatedOn BETWEEN ...
            e.HasIndex(s => new { s.Symbol, s.CreatedOn })
             .HasDatabaseName("IX_StrategyExecutionLog_Symbol_CreatedOn");
        });

        mb.Entity<StrategyPerformance>(e =>
        {
            e.Property(s => s.Strategy).HasMaxLength(20).IsRequired();
            e.Property(s => s.MarketState).HasMaxLength(30).IsRequired();
            e.Property(s => s.WinRate).HasPrecision(9, 4);
            e.Property(s => s.ProfitFactor).HasPrecision(18, 4);
            e.Property(s => s.Expectancy).HasPrecision(18, 2);
            e.Property(s => s.AverageWin).HasPrecision(18, 2);
            e.Property(s => s.AverageLoss).HasPrecision(18, 2);
            e.Property(s => s.NetPnL).HasPrecision(18, 2);
            e.Property(s => s.MaxDrawdown).HasPrecision(9, 4);

            // Upsert target — one row per bucket; also the read path for GetWinRateAsync.
            e.HasIndex(s => new { s.Strategy, s.MarketState, s.Expiry, s.Hour })
             .IsUnique()
             .HasDatabaseName("IX_StrategyPerformance_Bucket");
        });

        mb.Entity<IndicatorSnapshot>(e =>
        {
            e.Property(s => s.Symbol).HasMaxLength(20).IsRequired();

            e.Property(s => s.Ltp).HasPrecision(18, 2);
            e.Property(s => s.DayOpen).HasPrecision(18, 2);
            e.Property(s => s.YesterdayClose).HasPrecision(18, 2);

            e.Property(s => s.Ema20).HasPrecision(18, 4);
            e.Property(s => s.Ema50).HasPrecision(18, 4);
            e.Property(s => s.Ema20_5m).HasPrecision(18, 4);
            e.Property(s => s.Ema50_5m).HasPrecision(18, 4);
            e.Property(s => s.Ema9).HasPrecision(18, 4);
            e.Property(s => s.Ema21).HasPrecision(18, 4);

            e.Property(s => s.Rsi).HasPrecision(18, 4);
            e.Property(s => s.Rsi5m).HasPrecision(18, 4);
            e.Property(s => s.AtrRaw).HasPrecision(18, 4);
            e.Property(s => s.AtrPct).HasPrecision(18, 4);
            e.Property(s => s.Pcr).HasPrecision(18, 4);
            e.Property(s => s.Vwap).HasPrecision(18, 4);

            e.Property(s => s.PriceChangePct).HasPrecision(18, 4);

            e.Property(s => s.TrendDirection).HasMaxLength(20);
            e.Property(s => s.MarketRegime).HasMaxLength(30);
            e.Property(s => s.MarketState).HasMaxLength(30);
            e.Property(s => s.StrategySelected).HasMaxLength(30);
            e.Property(s => s.Confidence).HasPrecision(18, 2);
            e.Property(s => s.BlockedBy).HasMaxLength(100);
            e.Property(s => s.BlockedReason).HasMaxLength(500);

            e.Property(s => s.BlockedStrikePrice).HasPrecision(18, 2);
            e.Property(s => s.BlockedOptionType).HasMaxLength(2);
            e.Property(s => s.BlockedOptionSymbol).HasMaxLength(50);
            e.Property(s => s.BlockedDelta).HasPrecision(18, 4);
            e.Property(s => s.BlockedOptionLtp).HasPrecision(18, 2);
            e.Property(s => s.BlockedChangeInOi).HasPrecision(18, 2);

            // Matches the expected read pattern: WHERE Symbol=? ORDER BY SnapshotTime
            e.HasIndex(s => new { s.Symbol, s.SnapshotTime })
             .HasDatabaseName("IX_IndicatorSnapshot_Symbol_SnapshotTime");
        });

        // ── Forward outcome of an IndicatorSnapshot (+20 / -20 INDEX points) ──────────────────
        // Analytics only. Never read by signal generation, order placement or exit management.
        mb.Entity<IndicatorSnapshotOutcome>(e =>
        {
            e.Property(o => o.Symbol).HasMaxLength(20).IsRequired();
            e.Property(o => o.Direction).HasMaxLength(4).IsRequired();   // CALL / PUT

            e.Property(o => o.EntryPrice).HasPrecision(18, 2);
            e.Property(o => o.TargetPoints).HasPrecision(18, 2);
            e.Property(o => o.StopLossPoints).HasPrecision(18, 2);

            e.Property(o => o.FirstHitResult5Min).HasMaxLength(6);       // TARGET / SL / NONE
            e.Property(o => o.FirstHitResult10Min).HasMaxLength(6);
            e.Property(o => o.FirstHitResult15Min).HasMaxLength(6);

            e.Property(o => o.MaxFavorableMove5).HasPrecision(18, 2);
            e.Property(o => o.MaxAdverseMove5).HasPrecision(18, 2);
            e.Property(o => o.MaxFavorableMove10).HasPrecision(18, 2);
            e.Property(o => o.MaxAdverseMove10).HasPrecision(18, 2);
            e.Property(o => o.MaxFavorableMove15).HasPrecision(18, 2);
            e.Property(o => o.MaxAdverseMove15).HasPrecision(18, 2);

            e.Property(o => o.Anomaly).HasMaxLength(200);

            // One outcome per snapshot — makes re-running the backfill idempotent at the database
            // level, not merely by convention.
            e.HasIndex(o => o.IndicatorSnapshotId)
             .IsUnique()
             .HasDatabaseName("UX_IndicatorSnapshotOutcome_IndicatorSnapshotId");

            // Drives the worker's "what still needs calculating" scan.
            e.HasIndex(o => o.CompletedWindowMinutes)
             .HasDatabaseName("IX_IndicatorSnapshotOutcome_CompletedWindowMinutes");

            // Drives the analytics queries: GROUP BY Symbol, Direction.
            e.HasIndex(o => new { o.Symbol, o.Direction })
             .HasDatabaseName("IX_IndicatorSnapshotOutcome_Symbol_Direction");
        });

        mb.Entity<TrailingStopLog>(e =>
        {
            e.Property(t => t.Symbol).HasMaxLength(50).IsRequired();
            e.Property(t => t.OldOptionSL).HasPrecision(18, 2);
            e.Property(t => t.NewOptionSL).HasPrecision(18, 2);
            e.Property(t => t.AtrRaw).HasPrecision(18, 4);
            e.Property(t => t.Delta).HasPrecision(10, 6);
            e.Property(t => t.OptionAtrDistance).HasPrecision(18, 4);
            e.Property(t => t.TargetSL).HasPrecision(18, 2);
            e.Property(t => t.MaxStep).HasPrecision(18, 2);
            e.Property(t => t.MfePrice).HasPrecision(18, 2);

            // Matches the expected read pattern: WHERE SignalId=? ORDER BY CreatedOn
            e.HasIndex(t => new { t.SignalId, t.CreatedOn })
             .HasDatabaseName("IX_TrailingStopLog_SignalId_CreatedOn");
        });

        mb.Entity<StrategySetting>(e =>
        {
            e.Property(s => s.SettingKey).HasMaxLength(100).IsRequired();
            e.Property(s => s.Symbol).HasMaxLength(50);
            e.Property(s => s.SettingValue).HasPrecision(18, 6);
            e.Property(s => s.Description).HasMaxLength(500);

            // StrategySettingsService cache load: WHERE IsActive = 1
            e.HasIndex(s => s.IsActive).HasDatabaseName("IX_StrategySetting_IsActive");

            // One row per key+symbol (Symbol nullable = global) — upsert target
            e.HasIndex(s => new { s.SettingKey, s.Symbol }).IsUnique()
             .HasDatabaseName("IX_StrategySetting_SettingKey_Symbol");
        });
    }
}
