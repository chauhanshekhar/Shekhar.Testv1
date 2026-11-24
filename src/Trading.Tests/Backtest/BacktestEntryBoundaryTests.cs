using Trading.Backtest.Entry;
using Trading.Backtest.Settings;
using Trading.SignalEngine.Strategies;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>Boundaries of the real CALL/PUT chain, reached through the replay's EntryEvaluator (the live EvaluateCall / EvaluatePut).</summary>
public class BacktestEntryBoundaryTests
{
    private static readonly DateTime T = BtSupport.At(4, 30);      // 10:00 IST

    private static EntryEvaluator Eval(BacktestSettings? s = null) =>
        new(s ?? BtSupport.LiveLike(), BtSupport.Trading(), BtSupport.Instruments());

    [Theory]
    [InlineData(44.99, false)]
    [InlineData(45.0, true)]
    [InlineData(65.0, true)]
    [InlineData(65.01, false)]
    public void CallRsiBandIsInclusiveOn5mRsi(double rsi5, bool expected) =>
        Assert.Equal(expected, Eval().Evaluate(BtSupport.CallPoint("NIFTY", T, rsi5: rsi5), TradeSide.Call).Candidate);

    [Theory]
    [InlineData(5.0)]
    [InlineData(33.0)]
    [InlineData(95.0)]
    public void PutHasNoRsiCondition(double rsi5) =>
        Assert.True(Eval().Evaluate(BtSupport.PutPoint("NIFTY", T, rsi5: rsi5), TradeSide.Put).Candidate);

    [Fact]
    public void MissingFiveMinuteRsiFailsClosed()
    {
        var p = BtSupport.CallPoint("NIFTY", T) with { Rsi5m = null };
        Assert.False(Eval().Evaluate(p, TradeSide.Call).SignalProduced);
    }

    [Theory]
    [InlineData("NIFTY", 10.0, false)]
    [InlineData("NIFTY", 10.01, true)]
    [InlineData("SENSEX", 10.0, false)]
    [InlineData("SENSEX", 10.01, true)]
    [InlineData("BANKNIFTY", 27.5, false)]
    [InlineData("BANKNIFTY", 27.51, true)]
    public void EmaGapMustBeStrictlyGreaterThanTheSymbolThreshold(string symbol, double gap, bool expected) =>
        Assert.Equal(expected, Eval().Evaluate(BtSupport.CallPoint(symbol, T, gap: gap), TradeSide.Call).Candidate);

    [Theory]
    [InlineData(0.0299, false)]
    [InlineData(0.03, true)]
    [InlineData(0.0301, true)]
    public void AtrPercentGateIsInclusiveAtTheMinimum(double atrPct, bool expected)
    {
        var verdict = Eval().Evaluate(BtSupport.CallPoint("NIFTY", T, atrPct: atrPct), TradeSide.Call);
        Assert.Equal(expected, verdict.Candidate);
        Assert.True(verdict.SignalProduced);                    // the ATR gate runs after the chain: a quiet market still produced a signal
        if (!expected) Assert.Equal("AtrGate", verdict.BlockedBy);
    }

    [Theory]
    [InlineData(0.79, false)]
    [InlineData(0.80, true)]
    public void CallPcrMinimumIsInclusive(double pcr, bool expected) =>
        Assert.Equal(expected, Eval().Evaluate(BtSupport.CallPoint("NIFTY", T, pcr: pcr), TradeSide.Call).Candidate);

    [Theory]
    [InlineData("SENSEX", 0.69, false)]
    [InlineData("SENSEX", 0.70, true)]
    [InlineData("NIFTY", 0.10, true)]                           // no PCR rule for NIFTY or BANKNIFTY puts
    [InlineData("BANKNIFTY", 0.10, true)]
    public void PutPcrAppliesToSensexOnly(string symbol, double pcr, bool expected) =>
        Assert.Equal(expected, Eval().Evaluate(BtSupport.PutPoint(symbol, T, pcr: pcr), TradeSide.Put).Candidate);

    [Fact]
    public void MissingPcrIsReportedAsMissingDataNotAsAMarketBlock()
    {
        var v = Eval().Evaluate(BtSupport.CallPoint("NIFTY", T, pcr: null), TradeSide.Call);
        Assert.False(v.SignalProduced);
        Assert.Equal("DataUnavailable:Pcr", v.BlockedBy);
    }

    [Fact]
    public void TrendAndPriceVersusEma20AreBothRequired()
    {
        var below = BtSupport.CallPoint("NIFTY", T) with { Ltp = 23000 };           // LTP under EMA20
        Assert.False(Eval().Evaluate(below, TradeSide.Call).SignalProduced);
        Assert.False(Eval().Evaluate(BtSupport.PutPoint("NIFTY", T), TradeSide.Call).SignalProduced);   // wrong direction for the side
    }

    [Fact]
    public void EarlyTrendGateBlocksACallOnlyOncethe5mTrendIsAlreadyUp()
    {
        var s = BtSupport.LiveLike().Set("EnableEarlyTrendGate", null, 1).Set("EarlyTrendGateSide", null, 1);
        var early = BtSupport.CallPoint("NIFTY", T) with { Ema20_5m = 100, Ema50_5m = 200 };
        var late = BtSupport.CallPoint("NIFTY", T) with { Ema20_5m = 200, Ema50_5m = 100 };
        var unknown = BtSupport.CallPoint("NIFTY", T);                               // 5m EMAs not available: the gate lets it through

        Assert.True(Eval(s).Evaluate(early, TradeSide.Call).Candidate);
        Assert.Equal("EarlyTrendGate", Eval(s).Evaluate(late, TradeSide.Call).BlockedBy);
        Assert.True(Eval(s).Evaluate(unknown, TradeSide.Call).Candidate);
        var putLate = BtSupport.PutPoint("NIFTY", T) with { Ema20_5m = 100, Ema50_5m = 200 };
        Assert.True(Eval(s).Evaluate(putLate, TradeSide.Put).Candidate);              // side = CALL only
    }

    [Theory]
    [InlineData(6, 29, true)]      // 11:59 IST is 06:29 UTC
    [InlineData(6, 30, false)]     // 12:00 IST
    [InlineData(6, 59, false)]     // 12:29 IST
    [InlineData(7, 0, true)]       // 12:30 IST
    public void NoTradeWindowBlocksCallsBetweenNoonAndHalfPastTwelveIst(int hourUtc, int minuteUtc, bool allowed)
    {
        var s = BtSupport.LiveLike().Set("EnableNoTradeWindowGate", null, 1).Set("NoTradeWindowSide", null, 1)
            .Set("NoTradeWindowStartMinute", null, 720).Set("NoTradeWindowEndMinute", null, 750);
        Assert.Equal(allowed, Eval(s).Evaluate(BtSupport.CallPoint("NIFTY", BtSupport.At(hourUtc, minuteUtc)), TradeSide.Call).Candidate);
        // PUT is not covered by the CALL-only window
        Assert.True(Eval(s).Evaluate(BtSupport.PutPoint("NIFTY", BtSupport.At(hourUtc, minuteUtc)), TradeSide.Put).Candidate);
    }

    [Fact]
    public void IndexStopFloorBlocksAtrBelowTheFloor()
    {
        // default TradingSettings: floor = 20 / 1.5 = 13.33 index points
        Assert.Equal("IndexStopFloorGate", Eval().Evaluate(BtSupport.CallPoint("NIFTY", T, atr: 13.0), TradeSide.Call).BlockedBy);
        Assert.True(Eval().Evaluate(BtSupport.CallPoint("NIFTY", T, atr: 13.4), TradeSide.Call).Candidate);
    }

    // ── BANKNIFTY PUT breakdown: a separate eligibility path, evaluated only when the chain produced no signal ──────────
    // gap 10 is below the BANKNIFTY EMA-gap threshold (27.5), so the chain fails and the rule is the only way in; the chain has no PUT RSI condition.
    private static Trading.Backtest.Data.MarketPoint Breakdown(string symbol = "BANKNIFTY", double gap = 10) =>
        BtSupport.PutPoint(symbol, T, gap: gap, rsi5: 50) with
        {
            Rsi1m = 30, DayChangePct = -0.30, AtrPct = 0.04, Ema20_5m = 100, Ema50_5m = 200,
            MarketState = "Breakdown", TrendDirection = "Bearish",
        };

    [Fact]
    public void BankNiftyBreakdownRuleIsAnAlternativePathWhenTheChainFails()
    {
        var v = Eval().Evaluate(Breakdown(), TradeSide.Put);
        Assert.True(v.Candidate);
        Assert.Equal(SingleSignalRuleEvaluator.BankNiftyPutBreakdownRuleId, v.Path);
    }

    [Fact]
    public void WhenTheChainPassesTheRuleIsNotConsulted()
    {
        var p = Breakdown(gap: 40);
        Assert.Equal("AND_CHAIN", Eval().Evaluate(p, TradeSide.Put).Path);
    }

    [Fact]
    public void BreakdownRuleNeedsBankNiftyAndTheStoredMarketState()
    {
        Assert.False(Eval().Evaluate(Breakdown("NIFTY"), TradeSide.Put).SignalProduced);
        Assert.False(Eval().Evaluate(Breakdown() with { MarketState = null }, TradeSide.Put).SignalProduced);
        Assert.False(Eval().Evaluate(Breakdown() with { MarketState = "Trend" }, TradeSide.Put).SignalProduced);
        Assert.False(Eval().Evaluate(Breakdown() with { AtrPct = 0.034 }, TradeSide.Put).SignalProduced);
    }

    [Fact]
    public void BreakdownRuleKillSwitchStillWorksInTheReplay()
    {
        var off = BtSupport.LiveLike().Set(SingleSignalRuleEvaluator.EnabledKey, "BANKNIFTY", 0);
        Assert.False(Eval(off).Evaluate(Breakdown(), TradeSide.Put).SignalProduced);
    }

    [Fact]
    public void BreakdownRuleNeverCreatesACallSignal() =>
        Assert.False(Eval().Evaluate(Breakdown(), TradeSide.Call).SignalProduced);
}
