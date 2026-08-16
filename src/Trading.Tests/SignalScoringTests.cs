using Trading.Domain.Entities;
using Trading.Domain;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Tests.Support;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Covers the SIGNAL ENGINE FIX SPECIFICATION's section 25 test requirements against the real
/// SignalEngineService — either end-to-end via GenerateSignalDetailed (candidate-creation /
/// independence / threshold-outcome tests, which need the real option-chain/liquidity path) or
/// via the internal ComputeScoresForTest seam (pure score-component tests, which don't need a
/// realistic chain since each SignalScore field is independently computed).
///
/// All tests run with EnableSignalScoring=true (the scored path) — the legacy AND-gated path is
/// unchanged production code and is not what this spec is about.
/// </summary>
public class SignalScoringTests
{
    private const string Symbol = SignalEngineTestFactory.Symbol;

    private static FakeStrategySettingsService ScoredSettings() =>
        new FakeStrategySettingsService()
            .Set("EnableSignalScoring", 1)
            .Set("SignalScoreThreshold", 10)
            .Set("RsiCallFloor", 53).Set("RsiCallCeiling", 70)
            .Set("RsiPutFloor", 30).Set("RsiPutCeiling", 45)
            .Set("BodyPctMinimum", 30).Set("BodyPctScoreLowThreshold", 20)
            .Set("PriceChangeScoreThreshold", 0.15m)
            .Set("MinAtrPercent", 0.035m).Set("AtrScoreLowThreshold", 0.030m)
            .Set("PcrBullishThreshold", 1.00m).Set("PcrBearishThreshold", 1.00m);

    // ── 1. CALL candidate creation ──────────────────────────────────────────────
    [Fact]
    public void Call_WithBullishTrendAboveEmasAndRsi60_CreatesCallSignal()
    {
        var engine = SignalEngineTestFactory.Create(ScoredSettings());
        var chain = SignalEngineTestFactory.BuildChain(spot: 20000m, ceChangeInOi: 100);
        var candle = SignalEngineTestFactory.MakeCandle(open: 19980, close: 20010, high: 20020, low: 19970);

        var result = engine.GenerateSignalDetailed(
            symbol: Symbol, ltp: 20010m, ema20: 19990m, ema50: 19950m,
            pcr: 1.1m, rsi: 60m, currentVolume: 0, avgVolume20: 0,
            priceChangePct: 0.2m, atrPct: 0, atrRaw: 10m,
            optionChain: chain, ema20_5m: 19995m, ema50_5m: 19950m, lastClosedCandle: candle);

        Assert.NotNull(result.Signal);
        Assert.Equal(TradingConst.OptionType.CE, result.Signal!.OptionType);
    }

    // ── 2. PUT candidate creation ───────────────────────────────────────────────
    [Fact]
    public void Put_WithBearishTrendBelowEmasAndRsi38_CreatesPutSignal()
    {
        var engine = SignalEngineTestFactory.Create(ScoredSettings());
        var chain = SignalEngineTestFactory.BuildChain(spot: 20000m, peChangeInOi: 100);
        var candle = SignalEngineTestFactory.MakeCandle(open: 20020, close: 19990, high: 20030, low: 19980);

        var result = engine.GenerateSignalDetailed(
            symbol: Symbol, ltp: 19990m, ema20: 20010m, ema50: 20050m,
            pcr: 0.8m, rsi: 38m, currentVolume: 0, avgVolume20: 0,
            priceChangePct: -0.2m, atrPct: 0, atrRaw: 10m,
            optionChain: chain, ema20_5m: 20005m, ema50_5m: 20050m, lastClosedCandle: candle);

        Assert.NotNull(result.Signal);
        Assert.Equal(TradingConst.OptionType.PE, result.Signal!.OptionType);
    }

    // ── 3. CALL/PUT independence ────────────────────────────────────────────────
    // Every PUT condition is structurally false here (Trend=Bullish not Bearish, RSI=60 is
    // outside the PUT band, price change is positive not negative) — CALL still fires, proving
    // a valid CALL does not require any PUT condition to also hold.
    [Fact]
    public void Call_DoesNotRequireAnyPutConditionToHold()
    {
        var engine = SignalEngineTestFactory.Create(ScoredSettings());
        var chain = SignalEngineTestFactory.BuildChain(spot: 20000m, ceChangeInOi: 100);
        var candle = SignalEngineTestFactory.MakeCandle(open: 19980, close: 20010, high: 20020, low: 19970);

        var result = engine.GenerateSignalDetailed(
            symbol: Symbol, ltp: 20010m, ema20: 19990m, ema50: 19950m,
            pcr: 1.1m, rsi: 60m, currentVolume: 0, avgVolume20: 0,
            priceChangePct: 0.2m, atrPct: 0, atrRaw: 10m,
            optionChain: chain, ema20_5m: 19995m, ema50_5m: 19950m, lastClosedCandle: candle);

        Assert.NotNull(result.Signal);
        Assert.Equal(TradingConst.OptionType.CE, result.Signal!.OptionType);
    }

    // ── 4. RSI boundaries ────────────────────────────────────────────────────────
    [Theory]
    [InlineData(53, 2)]    // CALL floor — valid
    [InlineData(70, 2)]    // CALL ceiling — valid
    [InlineData(52.99, 0)] // just below CALL floor — invalid
    public void CallRsiScore_AtBoundary(double rsi, int expectedScore)
    {
        var (call, _) = ScoreOnly(rsi: (decimal)rsi);
        Assert.Equal(expectedScore, call.RsiScore);
    }

    [Theory]
    [InlineData(30, 2)]    // PUT floor — valid
    [InlineData(45, 2)]    // PUT ceiling — valid
    [InlineData(29.99, 0)] // just below PUT floor — invalid
    [InlineData(45.01, 0)] // just above PUT ceiling — invalid
    public void PutRsiScore_AtBoundary(double rsi, int expectedScore)
    {
        var (_, put) = ScoreOnly(rsi: (decimal)rsi);
        Assert.Equal(expectedScore, put.RsiScore);
    }

    // ── 5. Candle body scoring — quality score, never an automatic hard block ──────
    [Theory]
    [InlineData(35, 2)]
    [InlineData(25, 1)]
    [InlineData(10, 0)]
    public void BodyScore_AtBandBoundaries(double bodyPctTarget, int expectedScore)
    {
        // range=100 so bodyPct == the raw body points directly.
        var body = (decimal)bodyPctTarget;
        var candle = SignalEngineTestFactory.MakeCandle(open: 100, close: 100 + body, high: 100 + body, low: 100);
        var (call, _) = ScoreOnly(rsi: 60, candle: candle, bodyPct: body);
        Assert.Equal(expectedScore, call.BodyScore);
    }

    [Fact]
    public void LowBody_DoesNotAloneBlockAnOtherwiseStrongCandidate()
    {
        // Body <30% (BodyScore=0) but every other component maxed — must still reach threshold.
        var settings = ScoredSettings().Set("SignalScoreThreshold", 10);
        var engine = SignalEngineTestFactory.Create(settings);
        var chain = SignalEngineTestFactory.BuildChain(spot: 20000m, ceChangeInOi: 100);
        // Tiny body (1%) — well under both the 30% hard-block-in-legacy-mode bar and the 20% score band.
        var candle = SignalEngineTestFactory.MakeCandle(open: 20000, close: 20000.10m, high: 20050, low: 19950);

        var result = engine.GenerateSignalDetailed(
            symbol: Symbol, ltp: 20010m, ema20: 19990m, ema50: 19950m,
            pcr: 1.3m, rsi: 60m, currentVolume: 200, avgVolume20: 50,
            priceChangePct: 0.5m, atrPct: 0, atrRaw: 200m, // large atrRaw -> high atrPct
            optionChain: chain, ema20_5m: 19995m, ema50_5m: 19950m, lastClosedCandle: candle);

        Assert.NotNull(result.Signal);
    }

    // ── 6. ATR scoring ───────────────────────────────────────────────────────────
    [Theory]
    [InlineData(0.04, 2)]   // >= 0.035 high threshold
    [InlineData(0.032, 1)]  // within 0.030-0.0349 band
    [InlineData(0.02, 0)]   // below 0.030 low threshold
    public void AtrScore_AtBandBoundaries(double atrPct, int expectedScore)
    {
        var (call, _) = ScoreOnly(rsi: 60, atrPct: (decimal)atrPct);
        Assert.Equal(expectedScore, call.AtrScore);
    }

    // ── 7. Confidence scoring — reuses the existing ComputeConfidence formula, only the
    //      bucketing into +3/+2/+1/+0 is new. Inputs below are hand-traced against that formula
    //      (base 25 + EMA-separation band + PCR band + RSI band + volume band + candle-quality band).
    [Fact]
    public void ConfidenceScore_95_MapsToPlus3()
    {
        // emaSep>=0.20%(+20) + pcr>1.20(+15) + rsi>=70(+15) + volRatio>2(+10) + body>=min+30(+10) = 95
        var candle = SignalEngineTestFactory.MakeCandle(open: 100, close: 170, high: 170, low: 100); // bodyPct=70
        var (call, _) = ScoreOnly(
            ema20: 100.30m, ema50: 100m, pcr: 1.30m, rsi: 70m,
            currentVolume: 300, avgVolume20: 100, candle: candle, bodyPct: 70m, bodyPctMinimum: 35m);
        Assert.Equal(3, call.ConfidenceScore);
    }

    [Fact]
    public void ConfidenceScore_68_MapsToPlus2()
    {
        // emaSep>=0.20%(+20) + pcr in (1.05,1.20](+8) + rsi>=70(+15) + no volume(+0) + low body(+0) = 68
        var (call, _) = ScoreOnly(ema20: 100.30m, ema50: 100m, pcr: 1.10m, rsi: 70m);
        Assert.Equal(2, call.ConfidenceScore);
    }

    [Fact]
    public void ConfidenceScore_60_MapsToPlus1()
    {
        // emaSep>=0.20%(+20) + pcr>1.20(+15) + rsi<55 floor(+0) + no volume(+0) + low body(+0) = 60
        var (call, _) = ScoreOnly(ema20: 100.30m, ema50: 100m, pcr: 1.30m, rsi: 40m);
        Assert.Equal(1, call.ConfidenceScore);
    }

    [Fact]
    public void ConfidenceScore_Below60_MapsToPlus0()
    {
        // emaSep<0.05%(+0) + pcr<=1.05(+0) + rsi<55 floor(+0) + no volume(+0) + low body(+0) = 25
        var (call, _) = ScoreOnly(ema20: 100.01m, ema50: 100m, pcr: 1.00m, rsi: 40m);
        Assert.Equal(0, call.ConfidenceScore);
    }

    // ── 8. Threshold — 9 blocked, 10 eligible ──────────────────────────────────
    // Isolates CandleScore as the only component that differs between the two cases: with it at 0
    // the fixed remainder (Trend+EMA20+EMA50+RSI+PriceChange = 9) sits one point under the default
    // threshold of 10; flipping the candle bullish adds exactly +1 and clears it.
    [Fact]
    public void Score9_IsBlocked_Score10_IsEligible()
    {
        var chain = SignalEngineTestFactory.BuildChain(spot: 100m); // ChangeInOi=0 -> OiScore=0 both sides

        SignalGenerationResult Run(Candle candle) =>
            SignalEngineTestFactory.Create(ScoredSettings())
                .GenerateSignalDetailed(
                    symbol: Symbol, ltp: 20100m, ema20: 20001m, ema50: 20000m,
                    pcr: 0.90m, rsi: 60m, currentVolume: 0, avgVolume20: 0,
                    priceChangePct: 0.20m, atrPct: 0, atrRaw: 0.001m, // atrPct ~0 -> AtrScore=0
                    optionChain: chain, ema20_5m: 20001m, ema50_5m: 20000m, lastClosedCandle: candle);

        var flatCandle = SignalEngineTestFactory.MakeCandle(open: 100, close: 100, high: 100.5m, low: 99.5m);
        var blocked = Run(flatCandle);
        Assert.Null(blocked.Signal);
        Assert.Equal(9, blocked.Score?.Total);

        var bullishCandle = SignalEngineTestFactory.MakeCandle(open: 100.00m, close: 100.01m, high: 100.5m, low: 99.5m);
        var eligible = Run(bullishCandle);
        Assert.NotNull(eligible.Signal);
        Assert.Equal(10, eligible.Score?.Total);
    }

    // ── 9. Both candidates eligible — higher score wins ─────────────────────────
    // Spec's illustrative numbers are 12/10; these inputs are hand-traced to produce 15/10 (both
    // >= the threshold of 10, CALL strictly higher) — same comparison rule, concrete verified
    // numbers rather than hand-matching the spec's exact illustrative digits.
    [Fact]
    public void BothCandidatesEligible_HigherScoreWins()
    {
        var candle = SignalEngineTestFactory.MakeCandle(open: 100, close: 90, high: 101, low: 89); // bearish
        var atmCe = SignalEngineTestFactory.BuildChain(100)[0];
        var atmPe = SignalEngineTestFactory.BuildChain(100, peChangeInOi: 100)[1];

        var engine = SignalEngineTestFactory.Create(ScoredSettings());
        var (call, put) = engine.ComputeScoresForTest(
            symbol: Symbol, ltp: 20100m, ema20: 20050m, ema50: 20000m,
            ema20_5m: 20001m, ema50_5m: 20000m, // trendBullish
            pcr: 0.70m, rsi: 70m, currentVolume: 300, avgVolume20: 100,
            priceChangePct: 0m, atrPct: 0.05m,
            lastClosedCandle: candle, bodyPct: 40m, atmCe: atmCe, atmPe: atmPe,
            rsiCallFloor: 53m, rsiCallCeiling: 70m, rsiPutFloor: 30m, rsiPutCeiling: 45m,
            bodyPctMinimum: 35m, pcrBullishThreshold: 1.00m, pcrBearishThreshold: 1.00m);

        Assert.True(call.Total >= 10, $"CALL should be eligible, was {call.Total}");
        Assert.True(put.Total >= 10, $"PUT should be eligible, was {put.Total}");
        Assert.True(call.Total > put.Total, $"CALL ({call.Total}) should exceed PUT ({put.Total})");

        // Confirm the actual engine picks CALL, matching the score comparison above.
        var chain = new List<OptionChainData> { atmCe, atmPe };
        var result = engine.GenerateSignalDetailed(
            symbol: Symbol, ltp: 20100m, ema20: 20050m, ema50: 20000m,
            pcr: 0.70m, rsi: 70m, currentVolume: 300, avgVolume20: 100,
            priceChangePct: 0m, atrPct: 0, atrRaw: 20100m * 0.05m / 100m,
            optionChain: chain, ema20_5m: 20001m, ema50_5m: 20000m, lastClosedCandle: candle);

        Assert.NotNull(result.Signal);
        Assert.Equal(TradingConst.OptionType.CE, result.Signal!.OptionType);
    }

    // ── shared helper for the pure score-component tests ────────────────────────
    private static (SignalScore Call, SignalScore Put) ScoreOnly(
        decimal rsi,
        decimal ema20 = 100m, decimal ema50 = 100m,
        decimal pcr = 0m, decimal currentVolume = 0m, decimal avgVolume20 = 0m,
        decimal atrPct = 0m, Candle? candle = null, decimal bodyPct = 0m,
        decimal bodyPctMinimum = 35m)
    {
        var engine = SignalEngineTestFactory.Create(ScoredSettings());
        var chain = SignalEngineTestFactory.BuildChain(100m);
        candle ??= SignalEngineTestFactory.MakeCandle(open: 100, close: 100, high: 101, low: 99);

        return engine.ComputeScoresForTest(
            symbol: Symbol, ltp: 100m, ema20: ema20, ema50: ema50,
            ema20_5m: 0m, ema50_5m: 0m, // trend irrelevant to the fields under test here
            pcr: pcr, rsi: rsi, currentVolume: currentVolume, avgVolume20: avgVolume20,
            priceChangePct: 0m, atrPct: atrPct,
            lastClosedCandle: candle, bodyPct: bodyPct, atmCe: chain[0], atmPe: chain[1],
            rsiCallFloor: 53m, rsiCallCeiling: 70m, rsiPutFloor: 30m, rsiPutCeiling: 45m,
            bodyPctMinimum: bodyPctMinimum, pcrBullishThreshold: 1.00m, pcrBearishThreshold: 1.00m);
    }
}
