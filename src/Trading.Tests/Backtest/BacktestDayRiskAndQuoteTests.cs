using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Simulation;
using Trading.Backtest.Settings;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>
/// Live parity of the replay for: ConsecutiveLossGate and the daily loss limit (day-level stops), per-symbol lots from
/// StrategySettings AdaptiveLotMin (drives the partial exits), and real entry quotes from Signals / IndicatorSnapshots.
/// </summary>
public class BacktestDayRiskAndQuoteTests
{
    private const double Ltp = 23940;       // CallPoint's last price with the default geometry

    private static ExecutionSimulator Sim(BacktestSettings s, EntryQuoteIndex? quotes = null) =>
        new(new EntryEvaluator(s, BtSupport.Trading(), BtSupport.Instruments()), s, BtSupport.Exit(s), options: null, quotes);

    private static int Index(DateTime utc) => (int)(utc - BtSupport.SessionOpenUtc).TotalMinutes;

    /// <summary>Flat until t0, then 50 points lower for the rest of the day: every CALL entered from t0 on is stopped on its first bar.</summary>
    private static Bar[] DownFrom(DateTime t0) => BtSupport.Bars(i => i < Index(t0) ? Ltp : Ltp - 50);

    /// <summary>Entries 6 minutes apart (past the 5-minute same-side cooldown after each 1-minute stop).</summary>
    private static MarketPoint[] CallsEvery6Min(string symbol, DateTime t0, int count) =>
        Enumerable.Range(0, count).Select(k => BtSupport.CallPoint(symbol, t0.AddMinutes(6 * k))).ToArray();

    private static SimulationOptions Calls(DayRiskLimits? risk, PremiumMode mode = PremiumMode.ModelOnly) =>
        new(TradeSide.Call, StopModel.Live, mode, Costs: null, DayRisk: risk);

    private static DayRiskLimits Risk(decimal capital = 100_000m) => new(3, capital, 2m, IsPaper: false);

    [Fact]
    public void ThreeLossesInADayBlockTheFourthEntry()
    {
        var s = BtSupport.LiveLike().Set("EnableDailyLossLimit", null, 0);
        var t0 = BtSupport.At(4, 30);
        var r = Sim(s).Run([BtSupport.Data("NIFTY", DownFrom(t0), CallsEvery6Min("NIFTY", t0, 4))], Calls(Risk()));

        Assert.Equal(3, r.Trades.Count);
        Assert.All(r.Trades, t => Assert.True(t.PnlRs < 0));
        Assert.Equal(1, r.BlockedBy["ConsecutiveLossGate"]);
    }

    [Fact]
    public void WithoutDayRiskTheOldReplayKeepsTrading()
    {
        var s = BtSupport.LiveLike().Set("EnableDailyLossLimit", null, 0);
        var t0 = BtSupport.At(4, 30);
        var r = Sim(s).Run([BtSupport.Data("NIFTY", DownFrom(t0), CallsEvery6Min("NIFTY", t0, 4))], Calls(risk: null));

        Assert.Equal(4, r.Trades.Count);
    }

    [Fact]
    public void TheLossStreakBlocksOtherSymbolsToo()
    {
        // live reads TradeJournal for the day without a symbol filter
        var s = BtSupport.LiveLike().Set("EnableDailyLossLimit", null, 0);
        var t0 = BtSupport.At(4, 30);
        var r = Sim(s).Run(
        [
            BtSupport.Data("NIFTY", DownFrom(t0), CallsEvery6Min("NIFTY", t0, 3)),
            BtSupport.Data("SENSEX", BtSupport.FlatBars(Ltp), BtSupport.CallPoint("SENSEX", t0.AddMinutes(30))),
        ], Calls(Risk()));

        Assert.Equal(3, r.Trades.Count);
        Assert.All(r.Trades, t => Assert.Equal("NIFTY", t.Symbol));
        Assert.Equal(1, r.BlockedBy["ConsecutiveLossGate"]);
    }

    [Fact]
    public void TheGateSwitchedOffInStrategySettingsDoesNotBlock()
    {
        var s = BtSupport.LiveLike().Set("EnableDailyLossLimit", null, 0).Set("EnableConsecutiveLossGate", null, 0);
        var t0 = BtSupport.At(4, 30);
        var r = Sim(s).Run([BtSupport.Data("NIFTY", DownFrom(t0), CallsEvery6Min("NIFTY", t0, 4))], Calls(Risk()));

        Assert.Equal(4, r.Trades.Count);
        Assert.False(r.BlockedBy.ContainsKey("ConsecutiveLossGate"));
    }

    [Fact]
    public void MaxConsecutiveLossesIsReadFromStrategySettings()
    {
        var s = BtSupport.LiveLike().Set("EnableDailyLossLimit", null, 0).Set("MaxConsecutiveLosses", "NIFTY", 2);
        var t0 = BtSupport.At(4, 30);
        var r = Sim(s).Run([BtSupport.Data("NIFTY", DownFrom(t0), CallsEvery6Min("NIFTY", t0, 4))], Calls(Risk()));

        Assert.Equal(2, r.Trades.Count);
        Assert.Equal(2, r.BlockedBy["ConsecutiveLossGate"]);
    }

    [Fact]
    public void DailyLossBudgetStopsTradingOnceUsedUp()
    {
        // each stop = -15 premium points x lot 65 = -975 Rs; budget 50,000 x 2% = 1,000 -> the 3rd entry is refused
        var s = BtSupport.LiveLike();
        var t0 = BtSupport.At(4, 30);
        var r = Sim(s).Run([BtSupport.Data("NIFTY", DownFrom(t0), CallsEvery6Min("NIFTY", t0, 3))], Calls(Risk(capital: 50_000m)));

        Assert.Equal(2, r.Trades.Count);
        Assert.Equal(1, r.BlockedBy["DailyLossLimit"]);
    }

    [Fact]
    public void LotsComeFromStrategySettingsPerSymbolAndDrivePartialExits()
    {
        var s = BtSupport.LiveLike().Set("AdaptiveLotMin", "NIFTY", 2);
        var exit = BtSupport.Exit(s);

        Assert.Equal(2, exit.Symbols["NIFTY"].Lots);
        Assert.True(exit.Symbols["NIFTY"].PartialSaleAtArm);
        Assert.Equal(1, exit.Symbols["BANKNIFTY"].Lots);                 // appsettings default
        Assert.False(exit.Symbols["BANKNIFTY"].PartialSaleAtArm);         // one lot cannot be halved
    }

    [Fact]
    public void QuoteIndexNeverLooksAheadAndRespectsMaxAgeAndSide()
    {
        var t = new DateTime(2026, 10, 5, 11, 51, 0);
        var q = new EntryQuoteIndex(
        [
            new EntryQuoteRow { Symbol = "BANKNIFTY", TimeIst = t.AddSeconds(-30), OptionType = "PE", Ltp = 410, Delta = -0.48m, Origin = "Signals" },
            new EntryQuoteRow { Symbol = "BANKNIFTY", TimeIst = t.AddSeconds(30), OptionType = "PE", Ltp = 999, Delta = -0.5m, Origin = "Signals" },
            new EntryQuoteRow { Symbol = "BANKNIFTY", TimeIst = t, OptionType = "CE", Ltp = 300, Delta = 0.5m, Origin = "IndicatorSnapshots" },
        ]);

        var put = q.AtOrBefore("BANKNIFTY", TradeSide.Put, t);
        Assert.NotNull(put);
        Assert.Equal(410, put!.Ltp);                                     // the later 999 quote is in the future
        Assert.Equal(0.48, put.Delta, 6);                                // stored PE delta is negative: magnitude used

        Assert.Equal(300, q.AtOrBefore("BANKNIFTY", TradeSide.Call, t)!.Ltp);
        Assert.Null(q.AtOrBefore("BANKNIFTY", TradeSide.Call, t.AddMinutes(3)));   // older than 2 minutes
        Assert.Null(q.AtOrBefore("NIFTY", TradeSide.Call, t));
    }

    [Fact]
    public void AStoredEntryQuotePricesTheTradeInsteadOfTheAveragePremium()
    {
        var s = BtSupport.LiveLike();
        var t0 = BtSupport.At(4, 30);
        var quotes = new EntryQuoteIndex(
        [
            new EntryQuoteRow { Symbol = "NIFTY", TimeIst = t0 + SessionClock.IstOffset, OptionType = "CE", Ltp = 180, Delta = 0.4m, Origin = "Signals" },
        ]);

        var trade = Sim(s, quotes).Run([BtSupport.Data("NIFTY", DownFrom(t0), BtSupport.CallPoint("NIFTY", t0))],
            Calls(null, PremiumMode.OptionHistoryWhereAvailable)).Trades.Single();

        Assert.Equal(PremiumSource.DbEntryQuoteDeltaModel, trade.Source);
        Assert.Equal(180, trade.EntryPremium);
        Assert.Equal(-12 * 65, trade.PnlRs, 6);                          // R = 30 index points x real delta 0.4 = 12 premium points

        var model = Sim(s, quotes).Run([BtSupport.Data("NIFTY", DownFrom(t0), BtSupport.CallPoint("NIFTY", t0))],
            Calls(null, PremiumMode.ModelOnly)).Trades.Single();
        Assert.Equal(PremiumSource.IndexDeltaModel, model.Source);       // ModelOnly never reads the database quotes
    }
}
