using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// MarketDirectionGate: 5m EMA20/EMA50 vote across the indices (experiment, off by default).
/// </summary>
public class MarketDirectionGateTests
{
    private const string Call = TradingConst.SignalType.CALL, Put = TradingConst.SignalType.PUT;

    private static IReadOnlyList<(string Symbol, int? Direction)> Others(int? a, int? b) =>
        new List<(string, int?)> { ("BANKNIFTY", a), ("SENSEX", b) };

    private static string? Run(string type, int? own5m, int? own1m, int? a, int? b,
        int min = 2, bool align = false, bool neutral = true)
        => MarketDirectionGate.Evaluate(type, "NIFTY", own5m, own1m, Others(a, b), min, align, neutral);

    // ── score ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Put_AllThreeBearish_Proceeds() => Assert.Null(Run(Put, -1, -1, -1, -1));

    [Fact]
    public void Put_TwoOfThreeBearish_NetScoreMinus1_BlocksAtMinScore2_ProceedsAtMinScore1()
    {
        // 2 bearish + 1 bullish = -1: "weak bearish" in the proposal's table, so min score 2 blocks it
        Assert.NotNull(Run(Put, -1, -1, -1, 1));
        Assert.Null(Run(Put, -1, -1, -1, 1, min: 1));
    }

    [Fact]
    public void Put_TwoBearishOneFlat_ProceedsAtMinScore2WhenNeutralAllowed()
        => Assert.Null(Run(Put, -1, -1, -1, 0, neutral: false));

    [Fact]
    public void Put_OneBearishTwoBullish_Blocks()
    {
        var reason = Run(Put, -1, -1, 1, 1);

        Assert.NotNull(reason);
        Assert.Contains("PUT not confirmed", reason);
    }

    [Fact]
    public void Call_AllThreeBearish_Blocks() => Assert.NotNull(Run(Call, -1, 1, -1, -1));

    [Fact]
    public void Call_TwoOfThreeBullish_NetScorePlus1_BlocksAtMinScore2_ProceedsAtMinScore1()
    {
        Assert.NotNull(Run(Call, 1, 1, 1, -1));
        Assert.Null(Run(Call, 1, 1, 1, -1, min: 1));
    }

    [Fact]
    public void Call_AllThreeBullish_Proceeds() => Assert.Null(Run(Call, 1, 1, 1, 1));

    [Fact]
    public void MinScore3_NeedsAllThree()
    {
        Assert.NotNull(Run(Put, -1, -1, -1, 1, min: 3));
        Assert.Null(Run(Put, -1, -1, -1, -1, min: 3));
    }

    // ── neutral / missing data ───────────────────────────────────────────────────────────

    [Fact]
    public void NeutralIndex_BlocksWhenConfigured_ElseCountsAsZero()
    {
        Assert.NotNull(Run(Put, -1, -1, 0, -1, neutral: true));
        // neutral allowed: score -2 still meets 2
        Assert.Null(Run(Put, -1, -1, 0, -1, neutral: false));
    }

    [Fact]
    public void OwnFiveMinuteMissing_FailsOpen() => Assert.Null(Run(Put, null, -1, 1, 1));

    [Fact]
    public void OtherIndexMissing_VoteIsDropped_NotCountedAgainst()
    {
        // NIFTY and SENSEX bearish, BANKNIFTY has no data: 2 votes, needs min(2, 2) = 2 -> proceeds
        Assert.Null(Run(Put, -1, -1, null, -1));
        // NIFTY bearish, SENSEX bullish, BANKNIFTY no data: score 0 -> blocks
        Assert.NotNull(Run(Put, -1, -1, null, 1));
    }

    [Fact]
    public void OnlyOwnIndexCanVote_FailsOpen() => Assert.Null(Run(Put, -1, -1, null, null));

    // ── individual timeframe alignment ───────────────────────────────────────────────────

    [Fact]
    public void Alignment_Off_IgnoresOneMinuteConflict() => Assert.Null(Run(Put, -1, 1, -1, -1, align: false));

    [Fact]
    public void Alignment_On_OneMinuteAgainstTrade_Blocks()
    {
        var reason = Run(Put, -1, 1, -1, -1, align: true);

        Assert.NotNull(reason);
        Assert.Contains("1m EMA bullish / 5m EMA bearish", reason);
    }

    [Fact]
    public void Alignment_On_FlatOneMinute_Blocks() => Assert.NotNull(Run(Put, -1, 0, -1, -1, align: true));

    [Fact]
    public void Alignment_On_BothTimeframesInDirection_Proceeds() => Assert.Null(Run(Put, -1, -1, -1, -1, align: true));

    [Fact]
    public void Alignment_On_OneMinuteMissing_DoesNotBlock() => Assert.Null(Run(Put, -1, null, -1, -1, align: true));

    // ── Direction ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Direction_UsesSignAndTreatsZeroEmaAsMissing()
    {
        Assert.Equal(1, MarketDirectionGate.Direction(102m, 100m));
        Assert.Equal(-1, MarketDirectionGate.Direction(98m, 100m));
        Assert.Equal(0, MarketDirectionGate.Direction(100m, 100m));
        Assert.Null(MarketDirectionGate.Direction(0m, 100m));
        Assert.Null(MarketDirectionGate.Direction(100m, 0m));
    }

    // ── FiveMinuteDirection: closed bars, staleness ──────────────────────────────────────

    private static readonly DateTime Now = new(2026, 11, 18, 6, 32, 0, DateTimeKind.Utc);

    // Closes ramp so a fast EMA is above/below a slow one; the EMA func is a deterministic stand-in (last N mean).
    private static decimal Mean(List<decimal> closes, int p) => closes.TakeLast(p).Average();

    private static List<Candle> Bars(int count, decimal first, decimal step, int newestStartMinutesAgo = 10)
    {
        var list = new List<Candle>();
        for (var i = 0; i < count; i++)
            list.Add(new Candle
            {
                Symbol = "SENSEX_5m",
                CandleTime = Now.AddMinutes(-newestStartMinutesAgo - 5 * (count - 1 - i)),
                Close = first + step * i,
            });
        return list;
    }

    [Fact]
    public void FiveMinuteDirection_RisingSeries_IsBullish()
        => Assert.Equal(1, MarketDirectionGate.FiveMinuteDirection(Bars(60, 80000m, 10m), Now, 600, Mean));

    [Fact]
    public void FiveMinuteDirection_FallingSeries_IsBearish()
        => Assert.Equal(-1, MarketDirectionGate.FiveMinuteDirection(Bars(60, 82000m, -10m), Now, 600, Mean));

    [Fact]
    public void FiveMinuteDirection_TooFewBars_IsNull()
        => Assert.Null(MarketDirectionGate.FiveMinuteDirection(Bars(19, 80000m, 10m), Now, 600, Mean));

    [Fact]
    public void FiveMinuteDirection_StaleFeed_IsNull()
        => Assert.Null(MarketDirectionGate.FiveMinuteDirection(Bars(60, 80000m, 10m, newestStartMinutesAgo: 60), Now, 600, Mean));

    [Fact]
    public void FiveMinuteDirection_IgnoresTheFormingBar()
    {
        var bars = Bars(60, 80000m, 10m);                      // rising, newest closed bar started 10 min ago
        bars.Add(new Candle { Symbol = "SENSEX_5m", CandleTime = Now.AddMinutes(-2), Close = 1m });   // forming bar with a crash

        Assert.Equal(1, MarketDirectionGate.FiveMinuteDirection(bars, Now, 600, Mean));
    }
}
