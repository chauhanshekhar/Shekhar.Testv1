using Trading.Backtest.Configurations;
using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.SignalEngine.Strategies;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>The pasted gap-up continuation proposal: pullback, breakout, exhaustion filter and the counter-trend PUT block.</summary>
public class BacktestGapUpProposalTests
{
    private static readonly DateTime T = BtSupport.At(4, 30);

    private static EntryEvaluator Eval(EntryRules rules) =>
        new(BtSupport.LiveLike(), BtSupport.Trading(), BtSupport.Instruments(), SelectorMode.Ignore, rules);

    private static EntryRules Gap(bool pullback = false, bool breakout = false, double? exhaustion = null) => new()
    {
        GapTrendContinuation = true, GapUpPullback = pullback, GapUpBreakout = breakout, GapUpExhaustionAtr = exhaustion,
    };

    // CallPoint geometry: EMA50 23900, EMA20 23930, LTP 23940, ATR 20 (so 0.25 ATR = 5 pts, 1.5 ATR = 30 pts)
    private static MarketPoint Call(double gapPct = 0.45, bool bullish = true, double minLow = 23931, double maxHighPrior = 23938, double atr = 20) =>
        BtSupport.CallPoint("NIFTY", T, atr: atr) with
        {
            OpenGapPct = gapPct, Ema20_5m = 200, Ema50_5m = 100, MinLow10 = minLow, MaxHigh10Prior = maxHighPrior,
            LastClosedBar = bullish ? new Bar(T.AddMinutes(-1), 23935, 23942, 23934, 23940) : new Bar(T.AddMinutes(-1), 23945, 23946, 23938, 23940),
        };

    [Fact]
    public void GapUpModeAloneChangesNothingForTheChain() =>
        Assert.True(Eval(Gap()).Evaluate(Call(), TradeSide.Call).Candidate);

    [Fact]
    public void PullbackNeedsALowNearEma20AndABullishBar()
    {
        Assert.True(Eval(Gap(pullback: true)).Evaluate(Call(minLow: 23934), TradeSide.Call).Candidate);                  // low within 5 pts of EMA20
        Assert.Equal("GapUpPullback", Eval(Gap(pullback: true)).Evaluate(Call(minLow: 23936), TradeSide.Call).BlockedBy);   // never came back to EMA20
        Assert.Equal("GapUpPullback", Eval(Gap(pullback: true)).Evaluate(Call(minLow: 23934, bullish: false), TradeSide.Call).BlockedBy);
    }

    [Fact]
    public void BreakoutNeedsABullishCloseAboveThePriorTenBarHigh()
    {
        Assert.True(Eval(Gap(breakout: true)).Evaluate(Call(maxHighPrior: 23939), TradeSide.Call).Candidate);
        Assert.Equal("GapUpBreakout", Eval(Gap(breakout: true)).Evaluate(Call(maxHighPrior: 23940), TradeSide.Call).BlockedBy);   // equal is not a breakout
        Assert.Equal("GapUpBreakout", Eval(Gap(breakout: true)).Evaluate(Call(bullish: false, maxHighPrior: 23900), TradeSide.Call).BlockedBy);
    }

    [Theory]
    [InlineData(20, true)]      // LTP is 10 pts above EMA20; 1.5 x ATR 20 = 30
    [InlineData(6.67, true)]    // 1.5 x 6.67 = 10.005 >= 10
    [InlineData(6.0, false)]    // 1.5 x 6 = 9 < 10: too extended
    public void ExhaustionBlocksAnEntryMoreThan1_5AtrAboveEma20(double atr, bool allowed)
    {
        var p = Call(atr: atr);
        var verdict = Eval(Gap(exhaustion: 1.5)).Evaluate(p, TradeSide.Call);
        if (!allowed) Assert.Equal("GapUpExhaustion", verdict.BlockedBy);
        else Assert.NotEqual("GapUpExhaustion", verdict.BlockedBy);
    }

    [Fact]
    public void TheChecksOnlyApplyWhileTheGapTrendIsActive()
    {
        var strict = Gap(pullback: true, breakout: true, exhaustion: 1.5);
        Assert.NotEqual("GapUpPullback", Eval(strict).Evaluate(Call(gapPct: 0.10, minLow: 24000), TradeSide.Call).BlockedBy);   // gap too small: live behaviour
        Assert.NotEqual("GapUpBreakout", Eval(strict).Evaluate(Call(gapPct: 0.10, bullish: false), TradeSide.Call).BlockedBy);
    }

    // ── counter-trend PUTs while the gap-up trend is active ─────────────────────────────────────────────────────
    private static MarketPoint Put(string symbol, double gapPct) =>
        BtSupport.PutPoint(symbol, T, rsi5: 33) with { OpenGapPct = gapPct, Ema20_5m = 200, Ema50_5m = 100 };

    [Fact]
    public void ThePutChainIsBlockedInGapUpModeButNotOtherwise()
    {
        var rules = new EntryRules { GapTrendContinuation = true, GapUpBlocksPut = CounterTrendPut.BlockChain };
        Assert.Equal("GapUpCounterTrendPut", Eval(rules).Evaluate(Put("NIFTY", 0.45), TradeSide.Put).BlockedBy);
        Assert.True(Eval(rules).Evaluate(Put("NIFTY", 0.10), TradeSide.Put).Candidate);                                       // small gap
        Assert.True(Eval(new EntryRules { GapTrendContinuation = true }).Evaluate(Put("NIFTY", 0.45), TradeSide.Put).Candidate);   // block off
    }

    [Fact]
    public void TheBankNiftyBreakdownRuleCanNeverOverlapGapUpMode()
    {
        // the rule needs the 5m EMA20 BELOW the 5m EMA50; gap-up mode needs it ABOVE. So blocking the rule in gap-up mode can never remove a trade.
        var p = BtSupport.PutPoint("BANKNIFTY", T, gap: 10, rsi5: 50) with   // gap 10 < 27.5: the chain fails, the rule is the way in
        {
            Rsi1m = 30, DayChangePct = -0.30, AtrPct = 0.04, OpenGapPct = 0.45, Ema20_5m = 100, Ema50_5m = 200,
            MarketState = "Breakdown", TrendDirection = "Bearish",
        };
        var both = new EntryRules { GapTrendContinuation = true, GapUpBlocksPut = CounterTrendPut.BlockChainAndRule };
        Assert.Equal(SingleSignalRuleEvaluator.BankNiftyPutBreakdownRuleId, Eval(both).Evaluate(p, TradeSide.Put).Path);   // 5m down: gap-up mode off, rule untouched
    }

    [Fact]
    public void TheProposalCatalogHasTheThreeConfigurationsForCallAndTheBlockForPut()
    {
        var call = ConfigurationCatalog.BuildGapUpProposal(TradeSide.Call).Select(c => c.Name).ToList();
        foreach (var n in new[] { "CALL|G1", "CALL|G2", "CALL|G3", "CALL|NoPcr+G1", "CALL|Baseline" })
            Assert.Contains(n, call);
        var put = ConfigurationCatalog.BuildGapUpProposal(TradeSide.Put).Select(c => c.Name).ToList();
        Assert.Contains("PUT|BlockPutInGapUp:Chain", put);
        Assert.Contains("PUT|BlockPutInGapUp:ChainAndRule", put);
    }
}
