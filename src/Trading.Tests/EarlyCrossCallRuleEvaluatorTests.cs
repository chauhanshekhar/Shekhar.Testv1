using Trading.Domain.Entities;
using Trading.SignalEngine.Strategies;
using Trading.Tests.Fakes;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Paper-only early-cross CALL rule. The contract: no settings rows = never matches; any mode other than Paper never
/// matches; each condition has a boundary; a null 5m RSI fails closed; BarsSince1mUp counts closed bars since the 1m cross.
/// </summary>
public class EarlyCrossCallRuleEvaluatorTests
{
    private const string Sym = "BANKNIFTY";

    private static EarlyCrossCallRuleEvaluator With(params (string Key, decimal Value)[] rows)
    {
        var s = new FakeStrategySettingsService();
        foreach (var (k, v) in rows) s.Set(k, v);
        return new EarlyCrossCallRuleEvaluator(s);
    }

    private static EarlyCrossCallRuleEvaluator On() => With((EarlyCrossCallRuleEvaluator.EnableKey, 1m), ("EmaGapMinPoints", 15m));

    // A matching input unless a test overrides one field: 3 bars after the cross, gap 20, 5m not up, RSI 55, bullish bar, Paper.
    private static EarlyCrossInput Input(
        bool paper = true, int? bars = 3, decimal ltp = 54100m, decimal ema20 = 54080m, decimal ema50 = 54060m,
        decimal ema20_5m = 54000m, decimal ema50_5m = 54050m, decimal? rsi5m = 55m, bool bullish = true) =>
        new(paper, bars, ltp, ema20, ema50, ema20_5m, ema50_5m, rsi5m, bullish);

    [Fact]
    public void NoSettingsRows_NeverMatches()
    {
        var e = With();
        Assert.False(e.IsEnabled(Sym));
        Assert.False(e.Evaluate(Sym, Input()).Matched);
    }

    [Fact]
    public void FlagOnInPaper_AllConditionsHold_MatchesCallRule()
    {
        var r = On().Evaluate(Sym, Input());
        Assert.True(r.Matched);
        Assert.True(r.Call);
        Assert.Equal(EarlyCrossCallRuleEvaluator.RuleId, r.RuleId);
        Assert.Equal("CE", r.OptionType);
    }

    [Fact]
    public void FlagOnButNotPaper_NeverMatches()
    {
        var r = On().Evaluate(Sym, Input(paper: false));
        Assert.False(r.Matched);
        Assert.Contains("paper-only", r.Reason);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(10, true)]    // default maximum is inclusive
    [InlineData(11, false)]
    public void BarsSinceCross_Boundary(int bars, bool expected) =>
        Assert.Equal(expected, On().Evaluate(Sym, Input(bars: bars)).Matched);

    [Fact]
    public void UnknownBars_NoMatch() => Assert.False(On().Evaluate(Sym, Input(bars: null)).Matched);

    [Fact]
    public void MaxBarsSetting_IsRead()
    {
        var e = With((EarlyCrossCallRuleEvaluator.EnableKey, 1m), (EarlyCrossCallRuleEvaluator.MaxBarsKey, 5m), ("EmaGapMinPoints", 15m));
        Assert.True(e.Evaluate(Sym, Input(bars: 5)).Matched);
        Assert.False(e.Evaluate(Sym, Input(bars: 6)).Matched);
    }

    [Fact]
    public void PriceNotAboveEma20_NoMatch() => Assert.False(On().Evaluate(Sym, Input(ltp: 54080m)).Matched);

    [Fact]
    public void Ema20NotAboveEma50_NoMatch() => Assert.False(On().Evaluate(Sym, Input(ema20: 54060m, ema50: 54060m)).Matched);

    [Fact]
    public void BearishLastBar_NoMatch() => Assert.False(On().Evaluate(Sym, Input(bullish: false)).Matched);

    [Theory]
    [InlineData(44.9, false)]
    [InlineData(45.0, true)]
    [InlineData(70.0, true)]
    [InlineData(70.1, false)]
    public void Rsi5m_BandBoundaries(double rsi, bool expected) =>
        Assert.Equal(expected, On().Evaluate(Sym, Input(rsi5m: (decimal)rsi)).Matched);

    [Fact]
    public void NullRsi5m_FailsClosed() => Assert.False(On().Evaluate(Sym, Input(rsi5m: null)).Matched);

    [Fact]
    public void RsiBand_UsesTheCallChainSettings()
    {
        var e = With((EarlyCrossCallRuleEvaluator.EnableKey, 1m), ("EmaGapMinPoints", 15m), ("CallRsiFloor", 50m), ("CallRsiCeiling", 60m));
        Assert.False(e.Evaluate(Sym, Input(rsi5m: 49m)).Matched);
        Assert.True(e.Evaluate(Sym, Input(rsi5m: 55m)).Matched);
        Assert.False(e.Evaluate(Sym, Input(rsi5m: 61m)).Matched);
    }

    [Fact]
    public void FiveMinuteTrendAlreadyUp_NoMatch_UnlessRequirementTurnedOff()
    {
        var up = Input(ema20_5m: 54100m, ema50_5m: 54050m);
        Assert.False(On().Evaluate(Sym, up).Matched);

        var relaxed = With((EarlyCrossCallRuleEvaluator.EnableKey, 1m), (EarlyCrossCallRuleEvaluator.Require5mNotUpKey, 0m), ("EmaGapMinPoints", 15m));
        Assert.True(relaxed.Evaluate(Sym, up).Matched);
    }

    [Fact]
    public void Unavailable5mEmas_DoNotBlock() =>
        Assert.True(On().Evaluate(Sym, Input(ema20_5m: 0m, ema50_5m: 0m)).Matched);

    [Fact]
    public void EmaGapBelowMinimum_NoMatch_UnlessRequirementTurnedOff()
    {
        var narrow = Input(ema20: 54070m, ema50: 54060m);   // gap 10 < 15
        Assert.False(On().Evaluate(Sym, narrow).Matched);

        var relaxed = With((EarlyCrossCallRuleEvaluator.EnableKey, 1m), (EarlyCrossCallRuleEvaluator.RequireEmaGapKey, 0m), ("EmaGapMinPoints", 15m));
        Assert.True(relaxed.Evaluate(Sym, narrow).Matched);
    }

    [Fact]
    public void ParameterlessSingleSignalEvaluator_NeverSeesEarlyCross()
    {
        var e = new SingleSignalRuleEvaluator();
        Assert.False(e.IsEarlyCrossEnabled(Sym));
        Assert.False(e.EvaluateEarlyCross(Sym, Input()).Matched);
    }

    [Fact]
    public void SingleSignalEvaluatorWithSettings_FlagOffByDefault_AndMatchesWhenOn()
    {
        var off = new SingleSignalRuleEvaluator(new FakeStrategySettingsService());
        Assert.False(off.IsEarlyCrossEnabled(Sym));
        Assert.False(off.EvaluateEarlyCross(Sym, Input()).Matched);

        var on = new SingleSignalRuleEvaluator(new FakeStrategySettingsService()
            .Set(EarlyCrossCallRuleEvaluator.EnableKey, 1m).Set("EmaGapMinPoints", 15m));
        Assert.True(on.IsEarlyCrossEnabled(Sym));
        Assert.True(on.EvaluateEarlyCross(Sym, Input()).Matched);
    }

    // ── BarsSince1mUp ───────────────────────────────────────────────────────────────────────────────
    private static List<Candle> Candles(IEnumerable<decimal> closes)
    {
        var t = new DateTime(2026, 10, 27, 3, 45, 0, DateTimeKind.Utc);
        return closes.Select((c, i) => new Candle { Symbol = "BANKNIFTY_1m", Close = c, Open = c, High = c, Low = c, CandleTime = t.AddMinutes(i) }).ToList();
    }

    [Fact]
    public void BarsSince_FewerThan60Candles_Null() =>
        Assert.Null(EarlyCrossCallRuleEvaluator.BarsSince1mUp(Candles(Enumerable.Range(0, 59).Select(i => 54000m + i))));

    [Fact]
    public void BarsSince_FallingMarket_Null() =>
        Assert.Null(EarlyCrossCallRuleEvaluator.BarsSince1mUp(Candles(Enumerable.Range(0, 120).Select(i => 54000m - i))));

    [Fact]
    public void BarsSince_RunCoversWholeWindow_Null() =>
        Assert.Null(EarlyCrossCallRuleEvaluator.BarsSince1mUp(Candles(Enumerable.Range(0, 120).Select(i => 54000m + i))));

    [Fact]
    public void BarsSince_FreshTurnAfterDecline_CountsBarsSinceTheCross()
    {
        // 80 falling bars, then rising: EMA20 crosses above EMA50 some bars into the rise.
        var closes = Enumerable.Range(0, 80).Select(i => 54000m - i * 2m)
            .Concat(Enumerable.Range(1, 30).Select(i => 53840m + i * 6m)).ToList();

        // Independent reference: walk the same EMAs and note the bar index where the final up-run began.
        decimal e20 = closes[0], e50 = closes[0];
        var runStart = -1;
        for (var i = 1; i < closes.Count; i++)
        {
            e20 += 2m / 21m * (closes[i] - e20);
            e50 += 2m / 51m * (closes[i] - e50);
            if (e20 > e50) { if (runStart < 0) runStart = i; } else runStart = -1;
        }
        Assert.True(runStart > 0);

        var bars = EarlyCrossCallRuleEvaluator.BarsSince1mUp(Candles(closes));
        Assert.Equal(closes.Count - 1 - runStart, bars);
        Assert.True(bars < 30);
    }

    [Fact]
    public void BarsSince_TurnDownAtTheEnd_Null()
    {
        var closes = Enumerable.Range(0, 80).Select(i => 54000m + i)
            .Concat(Enumerable.Range(1, 60).Select(i => 54080m - i * 3m)).ToList();
        Assert.Null(EarlyCrossCallRuleEvaluator.BarsSince1mUp(Candles(closes)));
    }
}
