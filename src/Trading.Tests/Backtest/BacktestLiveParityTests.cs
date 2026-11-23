using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Indicators;
using Trading.Domain.Entities;
using Trading.Worker;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>
/// The replay must decide like production: the BreakoutProtectionService StrategyManager applies to every signal, the trigger
/// candle StrategyManager picks for the trading mode, and the session values MarketSnapshotBuilder feeds the CALL/PUT chain.
/// </summary>
public class BacktestLiveParityTests
{
    private static readonly DateTime T = BtSupport.At(4, 30);      // 10:00 IST

    private static Candle C(int minute, decimal open, decimal high, decimal low, decimal close) => new()
    {
        Symbol = "NIFTY", CandleTime = BtSupport.SessionOpenUtc.AddMinutes(minute),
        Open = open, High = high, Low = low, Close = close,
    };

    [Fact]
    public void BreakoutReplay_followsTheLiveStateMachine_breakoutPullbackContinuation()
    {
        var candles = new[]
        {
            C(0, 100, 110, 95, 105),     // initialises the session high 110 / low 95
            C(1, 105, 118, 104, 117),    // close 117 > 110 + 5: breakout, wait for a pullback
            C(2, 117, 117, 112, 113),    // low 112 <= 110 + 5: pullback, pullback high 117
            C(3, 113, 120, 113, 119),    // close 119 > 117 and > 110: continuation confirmed
        };

        var d = BreakoutProtectionService.ReplaySession(candles, isCall: true);

        Assert.False(d[0].Allowed);
        Assert.False(d[1].Allowed);
        Assert.False(d[2].Allowed);
        Assert.True(d[3].Allowed);
    }

    [Fact]
    public void BreakoutReplay_isIndependentPerCall_andDoesNotAllowWithoutABreakout()
    {
        var flat = Enumerable.Range(0, 30).Select(i => C(i, 100, 101, 99, 100)).ToArray();

        Assert.All(BreakoutProtectionService.ReplaySession(flat, isCall: true), x => Assert.False(x.Allowed));
        Assert.All(BreakoutProtectionService.ReplaySession(flat, isCall: false), x => Assert.False(x.Allowed));
    }

    private static global::Trading.Backtest.Settings.BacktestSettings BreakoutOn() =>
        BtSupport.LiveLike().Set("EnableBreakoutProtection", null, 1m);

    [Fact]
    public void BreakoutBlock_onThePoint_blocksAChainSignal_whenEnabledInStrategySettings()
    {
        var p = BtSupport.CallPoint("NIFTY", T) with { BreakoutCall = BreakoutDecision.Block("CALL waiting for day-high breakout") };

        var v = new EntryEvaluator(BreakoutOn(), BtSupport.Trading(), BtSupport.Instruments()).Evaluate(p, TradeSide.Call);

        Assert.True(v.SignalProduced);
        Assert.False(v.Candidate);
        Assert.Equal("BreakoutProtection", v.BlockedBy);
    }

    [Fact]
    public void BreakoutBlock_isIgnored_whenStrategySettingsHasNoEnableRow()
    {
        var p = BtSupport.CallPoint("NIFTY", T) with { BreakoutCall = BreakoutDecision.Block("CALL waiting for day-high breakout") };

        Assert.True(new EntryEvaluator(BtSupport.LiveLike(), BtSupport.Trading(), BtSupport.Instruments())
            .Evaluate(p, TradeSide.Call).Candidate);
    }

    [Fact]
    public void BreakoutBlock_isIgnored_whenStrategySettingsSwitchesItOff()
    {
        var p = BtSupport.CallPoint("NIFTY", T) with { BreakoutCall = BreakoutDecision.Block("CALL waiting for day-high breakout") };
        var settings = BtSupport.LiveLike().Set("EnableBreakoutProtection", null, 0m);

        Assert.True(new EntryEvaluator(settings, BtSupport.Trading(), BtSupport.Instruments())
            .Evaluate(p, TradeSide.Call).Candidate);
    }

    [Fact]
    public void BreakoutAllow_onThePoint_letsTheChainSignalThrough()
    {
        var p = BtSupport.CallPoint("NIFTY", T) with { BreakoutCall = BreakoutDecision.Allow("CALL continuation remains valid") };

        Assert.True(new EntryEvaluator(BtSupport.LiveLike(), BtSupport.Trading(), BtSupport.Instruments())
            .Evaluate(p, TradeSide.Call).Candidate);
    }

    [Fact]
    public void SkipBreakoutProtection_isAResearchOverride()
    {
        var p = BtSupport.CallPoint("NIFTY", T) with { BreakoutCall = BreakoutDecision.Block("blocked") };
        var ev = new EntryEvaluator(BreakoutOn(), BtSupport.Trading(), BtSupport.Instruments(),
            rules: new EntryRules { SkipBreakoutProtection = true });

        Assert.True(ev.Evaluate(p, TradeSide.Call).Candidate);
    }

    [Fact]
    public void PaperMode_triggersOnTodaysLastClosed5mBar_liveModeOnThe1mBar()
    {
        var settings = BtSupport.LiveLike();
        Assert.False(StrategyManager.UseOneMinuteTrigger(TradingMode.Paper, settings, "NIFTY"));
        Assert.True(StrategyManager.UseOneMinuteTrigger(TradingMode.Live, settings, "NIFTY"));

        var oneMinute = new Bar(T.AddMinutes(-1), 1, 2, 0.5, 1.5);
        var fiveMinute = new Bar(T.AddMinutes(-5), 10, 20, 5, 15);
        var p = BtSupport.CallPoint("NIFTY", T) with { LastClosedBar = oneMinute, LastClosed5mToday = fiveMinute };

        Assert.Equal(1.5m, EntryEvaluator.ToInputs(p, oneMinuteTrigger: true).LastClosedCandle!.Close);
        Assert.Equal(15m, EntryEvaluator.ToInputs(p, oneMinuteTrigger: false).LastClosedCandle!.Close);
        Assert.Null(EntryEvaluator.ToInputs(p with { LastClosed5mToday = null }, oneMinuteTrigger: false).LastClosedCandle);
    }

    [Fact]
    public void ChainInputs_useSessionValues_andDayChangeFromThePreviousClose()
    {
        var p = BtSupport.CallPoint("NIFTY", T) with
        {
            Ltp = 24100, DayOpen = 23990, DayLow = 23980, DayHigh = 24120, DayChangePct = 0.46,   // drifting window values
            SessionOpen = 24000, SessionLow = 23900, SessionHigh = 24150, PrevClose = 23800,
        };

        var m = EntryEvaluator.ToInputs(p);

        Assert.Equal(24000m, m.DayOpen);
        Assert.Equal(23900m, m.DayLow);
        Assert.Equal(24150m, m.DayHigh);
        Assert.Equal(23800m, m.YesterdayClose);
        Assert.Equal((decimal)((24100 - 23800) / 23800.0 * 100.0), m.DayChangePct);
        Assert.Equal(0.46m, m.PriceChangePct);   // production keeps the window-open PriceChangePct
    }

    [Fact]
    public void ChainInputs_fallBackToPointValues_whenSessionFieldsAreAbsent()
    {
        var p = BtSupport.CallPoint("NIFTY", T) with { DayChangePct = 0.25 };

        var m = EntryEvaluator.ToInputs(p);

        Assert.Equal((decimal)p.DayOpen, m.DayOpen);
        Assert.Equal((decimal)p.DayLow, m.DayLow);
        Assert.Equal((decimal)p.DayHigh, m.DayHigh);
        Assert.Equal(0.25m, m.DayChangePct);
    }

    [Fact]
    public void SeriesBuilder_attachesBreakoutDecisionsAndSessionFields_toEveryPoint()
    {
        var rnd = new Random(7);
        var bars = new List<Bar>();
        var price = 24000.0;
        foreach (var day in new[] { BtSupport.Day.AddDays(-1), BtSupport.Day })
            for (var i = 0; i < 375; i++)
            {
                var open = price;
                price += (rnd.NextDouble() - 0.5) * 12;
                bars.Add(new Bar(day.AddHours(3).AddMinutes(45 + i), open, Math.Max(open, price) + 1, Math.Min(open, price) - 1, price));
            }
        var five = new List<Bar>();
        for (var i = 0; i + 5 <= bars.Count; i += 5)
        {
            var g = bars.Skip(i).Take(5).ToArray();
            five.Add(new Bar(g[0].OpenUtc, g[0].Open, g.Max(x => x.High), g.Min(x => x.Low), g[4].Close));
        }

        var today = MarketSeriesBuilder.Build("NIFTY", bars, five).Where(p => p.TimeUtc.Date == BtSupport.Day).ToList();
        var prevClose = bars.Last(b => b.OpenUtc.Date < BtSupport.Day).Close;

        Assert.NotEmpty(today);
        Assert.All(today, p =>
        {
            Assert.NotNull(p.BreakoutCall);
            Assert.NotNull(p.BreakoutPut);
            Assert.Equal(prevClose, p.PrevClose);
            Assert.True(p.SessionHigh >= p.LastClosedBar.High);
            Assert.NotNull(p.LastClosed5mToday);
            Assert.True(p.LastClosed5mToday!.Value.OpenUtc.AddMinutes(5) <= p.TimeUtc);   // closed, no look-ahead
        });
    }
}
