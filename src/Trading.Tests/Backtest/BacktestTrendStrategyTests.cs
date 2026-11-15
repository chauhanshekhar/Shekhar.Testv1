using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Simulation;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>The two gap-up strategies: gap-and-go momentum and gap-hold pullback, each on its own entry path.</summary>
public class BacktestTrendStrategyTests
{
    private static readonly DateTime T = BtSupport.At(4, 30);     // 10:00 IST

    private static EntryEvaluator Eval(TrendStrategyRules rules) =>
        new(BtSupport.LiveLike(), BtSupport.Trading(), BtSupport.Instruments(), SelectorMode.Ignore, new EntryRules { Trend = rules });

    private static TrendStrategyRules Momentum(bool requireGap = true) =>
        new() { Strategy = TrendStrategy.Momentum, RequireGap = requireGap, MinUpFromOpenPct = requireGap ? 0 : 0.30, MinRsi5 = 60m };

    private static TrendStrategyRules Hold() =>
        new() { Strategy = TrendStrategy.GapHold, RequireGap = true, MinRsi5 = 50m, StartMinute = 9 * 60 + 20 };

    // CallPoint: EMA50 23900, EMA20 23930, LTP 23940, ATR 20. Session open 23800 after a gap from 23700 (gap 0.42%); RSI 72 (a HIGH RSI is wanted).
    private static MarketPoint P(double gapPct = 0.42, double rsi = 72, double sessionOpen = 23800, double sessionLow = 23790,
        bool bullish = true, double maxHighPrior = 23938, double minLow10 = 23934, double e20_5 = 200, double e50_5 = 100, DateTime? at = null) =>
        BtSupport.CallPoint("NIFTY", at ?? T, rsi5: rsi) with
        {
            OpenGapPct = gapPct, SessionOpen = sessionOpen, SessionLow = sessionLow, MaxHigh10Prior = maxHighPrior, MinLow10 = minLow10,
            Ema20_5m = e20_5, Ema50_5m = e50_5,
            LastClosedBar = bullish ? new Bar((at ?? T).AddMinutes(-1), 23935, 23942, 23934, 23940) : new Bar((at ?? T).AddMinutes(-1), 23945, 23946, 23938, 23940),
        };

    [Fact]
    public void MomentumEntersWhenTheRsiIsHighAndPriceBreaksTheRecentHigh()
    {
        var v = Eval(Momentum()).Evaluate(P(), TradeSide.Call);
        Assert.True(v.Candidate);
        Assert.Equal("TREND_MOMENTUM", v.Path);
    }

    [Fact]
    public void ARsiAboveTheProductionCeilingIsWhatTheStrategyWantsNotABlocker()
    {
        Assert.True(Eval(Momentum()).Evaluate(P(rsi: 85), TradeSide.Call).Candidate);              // production would block above 65
        Assert.False(new EntryEvaluator(BtSupport.LiveLike(), BtSupport.Trading(), BtSupport.Instruments()).Evaluate(P(rsi: 85), TradeSide.Call).Candidate);
    }

    [Theory]
    [InlineData(59.9, false)]
    [InlineData(60.0, true)]
    public void MomentumNeedsTheRsiToBeHigh(double rsi, bool expected) =>
        Assert.Equal(expected, Eval(Momentum()).Evaluate(P(rsi: rsi), TradeSide.Call).Candidate);

    [Fact]
    public void MomentumNeedsAGapAFreshBreakoutAndAnUpwardTrend()
    {
        Assert.False(Eval(Momentum()).Evaluate(P(gapPct: 0.10), TradeSide.Call).Candidate);                  // gap too small
        Assert.False(Eval(Momentum()).Evaluate(P(maxHighPrior: 23940), TradeSide.Call).Candidate);           // no close above the 10-bar high
        Assert.False(Eval(Momentum()).Evaluate(P(bullish: false), TradeSide.Call).Candidate);                // bearish last bar
        Assert.False(Eval(Momentum()).Evaluate(P(e20_5: 100, e50_5: 200), TradeSide.Call).Candidate);        // 5m trend down
        Assert.False(Eval(Momentum()).Evaluate(P(sessionOpen: 23950), TradeSide.Call).Candidate);            // price below the session open
    }

    [Fact]
    public void ThePriceMustAlsoBeAboveTheEmasLikeAnyTrendEntry()
    {
        var below = P() with { Ltp = 23920 };
        Assert.False(Eval(Momentum()).Evaluate(below, TradeSide.Call).Candidate);
    }

    [Theory]
    [InlineData(5, 20, false)]     // 05:20 UTC is 10:50 IST, before the 12:00-13:00 window
    public void TheEntryWindowIsRespected(int hourUtc, int minuteUtc, bool expected)
    {
        var rules = Momentum() with { StartMinute = 12 * 60, EndMinute = 13 * 60 };
        Assert.Equal(expected, Eval(rules).Evaluate(P(at: BtSupport.At(hourUtc, minuteUtc)), TradeSide.Call).Candidate);
    }

    [Fact]
    public void WithoutAGapRequirementTheMomentumEntryNeedsThePriceUpFromTheOpen()
    {
        var rules = Momentum(requireGap: false);
        Assert.True(Eval(rules).Evaluate(P(gapPct: 0.0), TradeSide.Call).Candidate);                          // 23940 vs 23800 = +0.59%
        Assert.False(Eval(rules).Evaluate(P(gapPct: 0.0, sessionOpen: 23930), TradeSide.Call).Candidate);     // +0.04% only
    }

    // ── GapHold ─────────────────────────────────────────────────────────────────────────────────────────────────
    // previous close = 23800 / 1.0042 = 23700.5; gap = 99.5 pts; half of it = 49.7 -> the low must stay >= 23750.2
    [Fact]
    public void GapHoldEntersOnAPullbackWhenTheGapHeld()
    {
        var v = Eval(Hold()).Evaluate(P(rsi: 55, sessionLow: 23760), TradeSide.Call);
        Assert.True(v.Candidate);
        Assert.Equal("TREND_GAPHOLD", v.Path);
    }

    [Fact]
    public void GapHoldRefusesAGapThatFilledMoreThanHalf() =>
        Assert.False(Eval(Hold()).Evaluate(P(rsi: 55, sessionLow: 23740), TradeSide.Call).Candidate);

    [Fact]
    public void GapHoldNeedsAPullbackTowardEma20WithABullishBar()
    {
        Assert.False(Eval(Hold()).Evaluate(P(rsi: 55, sessionLow: 23760, minLow10: 23960), TradeSide.Call).Candidate);   // never came back
        Assert.False(Eval(Hold()).Evaluate(P(rsi: 55, sessionLow: 23760, bullish: false), TradeSide.Call).Candidate);
    }

    [Fact]
    public void GapHoldDoesNotRequireAHighRsi()
    {
        Assert.True(Eval(Hold()).Evaluate(P(rsi: 50, sessionLow: 23760), TradeSide.Call).Candidate);
        Assert.False(Eval(Hold()).Evaluate(P(rsi: 49.9, sessionLow: 23760), TradeSide.Call).Candidate);
    }

    // ── shared safety gates and caps ────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void TheStrategiesStillObeyTheAtrAndIndexStopFloorGates()
    {
        Assert.Equal("AtrGate", Eval(Momentum()).Evaluate(BtSupport.CallPoint("NIFTY", T, atrPct: 0.01) with
        {
            OpenGapPct = 0.42, SessionOpen = 23800, SessionLow = 23790, MaxHigh10Prior = 23938, Ema20_5m = 200, Ema50_5m = 100, Rsi5m = 72,
            LastClosedBar = new Bar(T.AddMinutes(-1), 23935, 23942, 23934, 23940),
        }, TradeSide.Call).BlockedBy);
        Assert.Equal("IndexStopFloorGate", Eval(Momentum()).Evaluate(P() with { Atr = 10 }, TradeSide.Call).BlockedBy);
    }

    [Fact]
    public void ThePutSideIsNotTouchedByTheCallStrategies() =>
        Assert.False(Eval(Momentum()).Evaluate(BtSupport.PutPoint("NIFTY", T, rsi5: 80), TradeSide.Put).Candidate);

    [Fact]
    public void ASymbolDayTradeCapStopsFurtherEntries()
    {
        var s = BtSupport.LiveLike();
        var entry = new EntryEvaluator(s, BtSupport.Trading(), BtSupport.Instruments(), SelectorMode.Ignore, new EntryRules { Trend = Momentum() });
        var sim = new ExecutionSimulator(entry, s, BtSupport.Exit(s), options: null);
        var pts = Enumerable.Range(0, 4).Select(i => P(at: BtSupport.At(4, 30 + i * 10))).ToArray();
        // the crash right after each entry stops it out within a minute, so the one-position and cooldown gates do not interfere
        var bars = BtSupport.Bars(i => 23940 - 50 * (i % 2 == 0 ? 1 : 0));

        var capped = sim.Run([BtSupport.Data("NIFTY", bars, pts)],
            new SimulationOptions(TradeSide.Call, StopModel.Live, PremiumMode.ModelOnly, null, MaxTradesPerSymbolDay: 2));
        var uncapped = sim.Run([BtSupport.Data("NIFTY", bars, pts)],
            new SimulationOptions(TradeSide.Call, StopModel.Live, PremiumMode.ModelOnly, null));

        Assert.True(uncapped.Trades.Count > 2);
        Assert.Equal(2, capped.Trades.Count);
        Assert.True(capped.BlockedBy.GetValueOrDefault("MaxTradesPerSymbolDay") >= 1);
        Assert.Equal(capped.TotalSignals, capped.BlockedCount + capped.Trades.Count);
    }
}
