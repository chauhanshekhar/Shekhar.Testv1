using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Settings;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>The selector replay (MarketRegimeGate, MarketState routing, Sideways suppression) and the gap-trend experiments.</summary>
public class BacktestSelectorReplayTests
{
    private static readonly DateTime T = BtSupport.At(4, 30);

    private static EntryEvaluator Eval(EntryRules? rules = null, BacktestSettings? settings = null, SelectorMode mode = SelectorMode.Replay) =>
        new(settings ?? BtSupport.LiveLike(), BtSupport.Trading(), BtSupport.Instruments(), mode, rules);

    private static MarketPoint Call(string trend = "Uptrend", string vol = "High", string state = "Trend", double? gapPct = null,
        double e20_5 = 0, double e50_5 = 0, double rsi5 = 55) =>
        BtSupport.CallPoint("NIFTY", T, rsi5: rsi5) with
        {
            RegimeTrend = trend, RegimeVolatility = vol, ReplayedMarketState = state, OpenGapPct = gapPct, Ema20_5m = e20_5, Ema50_5m = e50_5,
        };

    [Fact]
    public void ADirectionalSignalPassesWhenTheRegimeIsATrend() =>
        Assert.True(Eval().Evaluate(Call(), TradeSide.Call).Candidate);

    [Fact]
    public void ASidewaysRegimeSuppressesDirectionalLikeProduction()
    {
        var v = Eval().Evaluate(Call(trend: "Sideways"), TradeSide.Call);
        Assert.True(v.SignalProduced);                       // the chain itself passed; the selector is what stopped it
        Assert.Equal("StrategySelector", v.BlockedBy);
    }

    [Fact]
    public void WithoutReplayTheSelectorIsNotConsulted() =>
        Assert.True(Eval(mode: SelectorMode.Ignore).Evaluate(Call(trend: "Sideways"), TradeSide.Call).Candidate);

    [Theory]
    [InlineData("Low")]
    [InlineData("Extreme")]
    public void MarketRegimeGateBlocksLowAndExtremeVolatilityEvenWithOverrides(string volatility)
    {
        var rules = new EntryRules { Sideways = SidewaysOverride.Always };
        var v = Eval(rules).Evaluate(Call(trend: "Sideways", vol: volatility), TradeSide.Call);
        Assert.Equal("MarketRegimeGate", v.BlockedBy);
    }

    [Theory]
    [InlineData("Range")]
    [InlineData("Reversal")]
    [InlineData("NoTrade")]
    [InlineData("News")]
    public void StatesWithoutAnEngineAreSuppressed(string state) =>
        Assert.Equal("StrategySelector", Eval().Evaluate(Call(state: state), TradeSide.Call).BlockedBy);

    [Theory]
    [InlineData("Trend")]
    [InlineData("StrongTrend")]
    [InlineData("Breakout")]
    [InlineData("Breakdown")]
    [InlineData("Scalping")]
    public void StatesThatRunDirectionalPass(string state) =>
        Assert.True(Eval().Evaluate(Call(state: state), TradeSide.Call).Candidate);

    [Fact]
    public void SidewaysOverrideWhenThe5mTrendAgreesOnlyAppliesWhenItDoes()
    {
        var rules = new EntryRules { Sideways = SidewaysOverride.When5mAgrees };
        Assert.True(Eval(rules).Evaluate(Call(trend: "Sideways", e20_5: 200, e50_5: 100), TradeSide.Call).Candidate);
        Assert.Equal("StrategySelector", Eval(rules).Evaluate(Call(trend: "Sideways", e20_5: 100, e50_5: 200), TradeSide.Call).BlockedBy);
        Assert.Equal("StrategySelector", Eval(rules).Evaluate(Call(trend: "Sideways"), TradeSide.Call).BlockedBy);   // 5m EMAs unknown: no override
    }

    [Fact]
    public void SidewaysAlwaysOverridesWithoutLookingAtThe5mTrend() =>
        Assert.True(Eval(new EntryRules { Sideways = SidewaysOverride.Always }).Evaluate(Call(trend: "Sideways"), TradeSide.Call).Candidate);

    // ── gap-trend continuation ───────────────────────────────────────────────────────────────────────────────────
    private static EntryRules GapRule(decimal? callCeiling = null, bool sideways = false, bool earlyTrend = false, bool emaGap = false) => new()
    {
        GapTrendContinuation = true, GapTrendCallRsiCeiling = callCeiling, GapTrendSkipsSidewaysSuppression = sideways,
        GapTrendSkipsEarlyTrendGate = earlyTrend, GapTrendWaivesEmaGapFilter = emaGap,
    };

    [Fact]
    public void TheWiderRsiCeilingApplies_OnlyWhileTheGapTrendIsActive()
    {
        var rule = GapRule(callCeiling: 85m);
        Assert.True(Eval(rule).Evaluate(Call(rsi5: 80, gapPct: 0.45, e20_5: 200, e50_5: 100), TradeSide.Call).Candidate);    // gap up, 5m up
        Assert.False(Eval(rule).Evaluate(Call(rsi5: 80, gapPct: 0.10, e20_5: 200, e50_5: 100), TradeSide.Call).Candidate);   // gap too small
        Assert.False(Eval(rule).Evaluate(Call(rsi5: 80, gapPct: 0.45, e20_5: 100, e50_5: 200), TradeSide.Call).Candidate);   // 5m trend not up
        Assert.False(Eval(rule).Evaluate(Call(rsi5: 80, gapPct: null, e20_5: 200, e50_5: 100), TradeSide.Call).Candidate);   // no gap known
        Assert.False(Eval(rule).Evaluate(Call(rsi5: 86, gapPct: 0.45, e20_5: 200, e50_5: 100), TradeSide.Call).Candidate);   // still capped at 85
    }

    [Fact]
    public void AGapDownDoesNotActivateTheCallRule() =>
        Assert.False(Eval(GapRule(callCeiling: 85m)).Evaluate(Call(rsi5: 80, gapPct: -0.45, e20_5: 200, e50_5: 100), TradeSide.Call).Candidate);

    [Fact]
    public void TheGapTrendRuleCanSkipTheSidewaysSuppressionAndTheEarlyTrendGate()
    {
        var settings = BtSupport.LiveLike().Set("EnableEarlyTrendGate", null, 1).Set("EarlyTrendGateSide", null, 1);
        var p = Call(trend: "Sideways", gapPct: 0.45, e20_5: 200, e50_5: 100);

        Assert.Equal("StrategySelector", Eval(GapRule(), settings).Evaluate(p, TradeSide.Call).BlockedBy);
        Assert.Equal("EarlyTrendGate", Eval(GapRule(sideways: true), settings).Evaluate(p, TradeSide.Call).BlockedBy);
        Assert.True(Eval(GapRule(sideways: true, earlyTrend: true), settings).Evaluate(p, TradeSide.Call).Candidate);
    }

    [Fact]
    public void TheGapTrendRuleCanWaiveTheEmaGapFilter()
    {
        var narrow = BtSupport.CallPoint("NIFTY", T, gap: 4) with { RegimeTrend = "Uptrend", RegimeVolatility = "High", ReplayedMarketState = "Trend", OpenGapPct = 0.45, Ema20_5m = 200, Ema50_5m = 100 };
        Assert.False(Eval(GapRule()).Evaluate(narrow, TradeSide.Call).Candidate);                    // gap 4 points <= 10: blocked
        Assert.True(Eval(GapRule(emaGap: true)).Evaluate(narrow, TradeSide.Call).Candidate);
    }

    [Fact]
    public void WithoutTheRulesEnabledProductionIsReproduced()
    {
        var p = Call(rsi5: 80, gapPct: 0.45, e20_5: 200, e50_5: 100);
        Assert.False(Eval(new EntryRules()).Evaluate(p, TradeSide.Call).Candidate);
        Assert.False(Eval(null).Evaluate(p, TradeSide.Call).Candidate);
    }

    [Fact]
    public void WhenTheSelectorSuppressesBankNiftyPutTheApprovedRuleStillGetsItsChance()
    {
        // chain passes (PUT RSI 33) but the regime is Sideways; the breakdown rule matches on the replayed MarketState
        var p = BtSupport.PutPoint("BANKNIFTY", T, gap: 40, rsi5: 33) with
        {
            Rsi1m = 30, DayChangePct = -0.30, AtrPct = 0.04, Ema20_5m = 100, Ema50_5m = 200,
            RegimeTrend = "Sideways", RegimeVolatility = "High", ReplayedMarketState = "Breakdown", TrendDirection = "Bearish",
        };
        var v = Eval().Evaluate(p, TradeSide.Put);
        Assert.True(v.Candidate);
        Assert.Equal(Trading.SignalEngine.Strategies.SingleSignalRuleEvaluator.BankNiftyPutBreakdownRuleId, v.Path);
    }
}
