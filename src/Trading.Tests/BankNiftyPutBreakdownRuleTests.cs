using Trading.Domain;
using Trading.Domain.Entities;
using Trading.SignalEngine.Strategies;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The BANKNIFTY PUT BREAKDOWN rule, condition by condition.
///
/// Backtest this rule was approved on: 448 samples, 5m 73.66% / 10m 88.17% / 15m 94.64% win rate.
/// Every threshold below is one of the values that win rate was measured at, so these tests exist
/// to catch a silent drift between the live rule and the validated one — including the boundary
/// direction (inclusive vs exclusive), which is where such drift usually hides.
///
/// Confidence is deliberately absent from the rule. Two tests below pin that explicitly.
/// </summary>
public class BankNiftyPutBreakdownRuleTests
{
    private static readonly SingleSignalRuleEvaluator Evaluator = new();

    /// <summary>A snapshot satisfying every condition, with headroom on each so a single
    /// mutation in a test isolates exactly one condition.</summary>
    private static IndicatorSnapshot Matching() => new()
    {
        Symbol = "BANKNIFTY",
        TrendDirection = "Bearish",
        MarketState = "Breakdown",
        Ltp = 49_800m,
        Ema20 = 49_900m,
        Ema50 = 50_000m,
        Ema20_5m = 49_950m,
        Ema50_5m = 50_050m,
        Rsi = 30m,
        PriceChangePct = -0.45m,
        AtrPct = 0.050m,
        Pcr = 0.85m,
    };

    // ── Baseline ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void MatchingSnapshot_ProducesPutSignalWithRuleId()
    {
        var result = Evaluator.Evaluate(Matching());

        Assert.True(result.Matched);
        Assert.Equal("BANKNIFTY_PUT_BREAKDOWN", result.RuleId);
        Assert.Equal(TradingConst.OptionType.PE, result.OptionType);
        Assert.False(result.Call);          // PUT, never CALL
        Assert.Null(result.Reason);
    }

    // ── Confidence is NOT part of this rule ─────────────────────────────────────────────────

    [Theory]
    [InlineData(46)]   // the live situation this rule exists for
    [InlineData(20)]
    [InlineData(0)]
    public void LowConfidence_DoesNotBlockTheRule(int confidence)
    {
        var s = Matching();
        s.Confidence = confidence;

        var result = Evaluator.Evaluate(s);

        Assert.True(result.Matched,
            $"Confidence {confidence} must not suppress a validated breakdown — confidence is not one of the rule's conditions");
        Assert.Equal("BANKNIFTY_PUT_BREAKDOWN", result.RuleId);
    }

    // ── RSI <= 35 ───────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("35.00", true)]
    [InlineData("35.01", false)]
    [InlineData("34.99", true)]
    public void RsiBoundary(string rsi, bool expected)
    {
        var s = Matching();
        s.Rsi = decimal.Parse(rsi, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, Evaluator.Evaluate(s).Matched);
    }

    // ── PriceChangePct <= -0.20 ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("-0.20", true)]
    [InlineData("-0.1999", false)]
    [InlineData("-0.2001", true)]
    public void PriceChangePctBoundary(string priceChangePct, bool expected)
    {
        var s = Matching();
        s.PriceChangePct = decimal.Parse(priceChangePct, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, Evaluator.Evaluate(s).Matched);
    }

    // ── AtrPct >= 0.035 ─────────────────────────────────────────────────────────────────────
    // Raised from 0.030 on 2026-09-04. Verified against production first: of every BANKNIFTY
    // snapshot satisfying the full rule, NONE sat in the 0.030-0.035 band, so the tightening
    // discards no historical match.
    [Theory]
    [InlineData("0.0349", false)]
    [InlineData("0.035", true)]
    [InlineData("0.0351", true)]
    [InlineData("0.030", false)]    // the old floor - now rejected
    public void AtrPctBoundary(string atrPct, bool expected)
    {
        var s = Matching();
        s.AtrPct = decimal.Parse(atrPct, System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal(expected, Evaluator.Evaluate(s).Matched);
    }

    // ── Ltp < Ema20 (strict) ────────────────────────────────────────────────────────────────

    [Fact]
    public void LtpJustBelowEma20_Matches()
    {
        var s = Matching();
        s.Ltp = s.Ema20 - 0.01m;

        Assert.True(Evaluator.Evaluate(s).Matched);
    }

    [Fact]
    public void LtpEqualToEma20_DoesNotMatch()
    {
        var s = Matching();
        s.Ltp = s.Ema20;

        var result = Evaluator.Evaluate(s);
        Assert.False(result.Matched);
        Assert.Contains("Ema20", result.Reason);
    }

    // ── Ema20 < Ema50 (strict) ──────────────────────────────────────────────────────────────

    [Fact]
    public void Ema20BelowEma50_Matches()
    {
        var s = Matching();
        s.Ema20 = s.Ema50 - 0.01m;
        s.Ltp = s.Ema20 - 1m;

        Assert.True(Evaluator.Evaluate(s).Matched);
    }

    [Fact]
    public void Ema20EqualToEma50_DoesNotMatch()
    {
        var s = Matching();
        s.Ema20 = s.Ema50;

        Assert.False(Evaluator.Evaluate(s).Matched);
    }

    // ── Ema20_5m < Ema50_5m (strict) ────────────────────────────────────────────────────────

    [Fact]
    public void Ema20_5mBelowEma50_5m_Matches()
    {
        var s = Matching();
        s.Ema20_5m = s.Ema50_5m - 0.01m;

        Assert.True(Evaluator.Evaluate(s).Matched);
    }

    [Fact]
    public void Ema20_5mEqualToEma50_5m_DoesNotMatch()
    {
        var s = Matching();
        s.Ema20_5m = s.Ema50_5m;

        Assert.False(Evaluator.Evaluate(s).Matched);
    }

    // ── TrendDirection ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Bearish", true)]
    [InlineData("Bullish", false)]
    [InlineData("Sideways", false)]
    [InlineData("", false)]
    public void TrendDirectionRequirement(string trendDirection, bool expected)
    {
        var s = Matching();
        s.TrendDirection = trendDirection;

        Assert.Equal(expected, Evaluator.Evaluate(s).Matched);
    }

    // ── MarketState ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Breakdown", true)]
    [InlineData("Breakout", false)]
    [InlineData("StrongTrend", false)]   // BANKNIFTY PUT_STRONG_TREND was NOT approved
    [InlineData("Trend", false)]
    [InlineData("Scalping", false)]
    [InlineData("", false)]
    public void MarketStateRequirement(string marketState, bool expected)
    {
        var s = Matching();
        s.MarketState = marketState;

        Assert.Equal(expected, Evaluator.Evaluate(s).Matched);
    }

    // ── Symbol ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("BANKNIFTY", true)]
    [InlineData("NIFTY", false)]        // NIFTY PUT_STRONG_TREND was NOT approved
    [InlineData("SENSEX", false)]       // SENSEX PUT_STRONG_TREND was NOT approved
    [InlineData("FINNIFTY", false)]
    [InlineData("MIDCAPNIFTY", false)]
    public void SymbolRequirement(string symbol, bool expected)
    {
        var s = Matching();
        s.Symbol = symbol;

        Assert.Equal(expected, Evaluator.Evaluate(s).Matched);
    }

    // ── Only this rule is approved ──────────────────────────────────────────────────────────

    [Fact]
    public void BullishBreakout_NeverMatches()
    {
        // BANKNIFTY CALL_BREAKOUT was explicitly NOT approved — the evaluator must have no
        // CALL path at all, not merely a stricter one.
        var s = Matching();
        s.TrendDirection = "Bullish";
        s.MarketState = "Breakout";
        s.Ltp = 50_200m;
        s.Ema20 = 50_100m;
        s.Ema50 = 50_000m;
        s.Ema20_5m = 50_150m;
        s.Ema50_5m = 50_050m;
        s.Rsi = 68m;
        s.PriceChangePct = 0.45m;

        Assert.False(Evaluator.Evaluate(s).Matched);
    }

    [Fact]
    public void NoMatch_ReportsFirstFailedConditionForDebugLogging()
    {
        var s = Matching();
        s.Rsi = 40m;

        var result = Evaluator.Evaluate(s);

        Assert.False(result.Matched);
        Assert.Null(result.RuleId);
        Assert.Contains("RSI", result.Reason);
        Assert.Contains("35", result.Reason);
    }
}
