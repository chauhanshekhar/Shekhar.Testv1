using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Reporting;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The day-type classifier. Every case builds two sessions: a flat reference day that sets the
/// previous close, then the day under test, so the gap is controlled exactly.
/// </summary>
public class SessionClassifierTests
{
    private const double PrevCloseLevel = 50_000;

    /// <summary>A session of 1m bars walking linearly from open to close, with the given extremes.</summary>
    private static IEnumerable<Bar> Session(DateOnly date, double open, double close, double high, double low, int bars = 60)
    {
        // IST 09:15 is 03:45 UTC.
        var start = date.ToDateTime(new TimeOnly(3, 45));
        for (var i = 0; i < bars; i++)
        {
            var a = open + (close - open) * i / (double)(bars - 1);
            var b = open + (close - open) * (i + 1) / (double)(bars - 1);
            // Put the day's extremes on the middle bar so open/close stay exact.
            var h = i == bars / 2 ? high : Math.Max(a, b);
            var l = i == bars / 2 ? low : Math.Min(a, b);
            yield return new Bar(start.AddMinutes(i), a, h, l, b);
        }
    }

    private static SessionProfile Classify(double open, double close, double high, double low)
    {
        var day0 = new DateOnly(2026, 10, 12);
        var day1 = new DateOnly(2026, 10, 13);
        var bars = Session(day0, PrevCloseLevel, PrevCloseLevel, PrevCloseLevel, PrevCloseLevel)
            .Concat(Session(day1, open, close, high, low))
            .ToList();
        return SessionClassifier.Classify("BANKNIFTY", bars).Single(p => p.Date == day1);
    }

    [Fact]
    public void GapDownThatKeepsFalling_isContinuation()
    {
        var p = Classify(open: 49_700, close: 49_200, high: 49_750, low: 49_150);
        Assert.Equal(OpeningType.GapDown, p.Opening);
        Assert.Equal(SessionPath.Continuation, p.Path);
    }

    [Fact]
    public void GapDownThatRecovers_isReversal()
    {
        var p = Classify(open: 49_700, close: 50_200, high: 50_250, low: 49_650);
        Assert.Equal(OpeningType.GapDown, p.Opening);
        Assert.Equal(SessionPath.Reversal, p.Path);
    }

    [Fact]
    public void GapUpThatKeepsRising_isContinuation()
    {
        var p = Classify(open: 50_300, close: 50_800, high: 50_850, low: 50_250);
        Assert.Equal(OpeningType.GapUp, p.Opening);
        Assert.Equal(SessionPath.Continuation, p.Path);
    }

    [Fact]
    public void GapUpThatSellsOff_isReversal()
    {
        var p = Classify(open: 50_300, close: 49_800, high: 50_350, low: 49_750);
        Assert.Equal(OpeningType.GapUp, p.Opening);
        Assert.Equal(SessionPath.Reversal, p.Path);
    }

    [Fact]
    public void NarrowSession_isRange_whicheverWayItClosed()
    {
        // Whole range 0.1%, far below RangeMaxPct.
        var p = Classify(open: 49_700, close: 49_680, high: 49_720, low: 49_670);
        Assert.Equal(OpeningType.GapDown, p.Opening);
        Assert.Equal(SessionPath.Range, p.Path);
    }

    [Fact]
    public void WideSessionThatRoundTrips_isRange_notATrend()
    {
        // 2% of travel but closes 0.02% from the open: it went nowhere.
        var p = Classify(open: 49_700, close: 49_710, high: 50_200, low: 49_200);
        Assert.Equal(SessionPath.Range, p.Path);
    }

    [Fact]
    public void NormalOpen_usesTrendLabels_notContinuation()
    {
        var p = Classify(open: 50_050, close: 50_600, high: 50_650, low: 50_000);
        Assert.Equal(OpeningType.Normal, p.Opening);
        Assert.Equal(SessionPath.TrendUp, p.Path);

        var q = Classify(open: 50_050, close: 49_500, high: 50_100, low: 49_450);
        Assert.Equal(OpeningType.Normal, q.Opening);
        Assert.Equal(SessionPath.TrendDown, q.Path);
    }

    [Fact]
    public void FirstSessionInTheData_hasNoPreviousClose_soIsUnknown()
    {
        var day = new DateOnly(2026, 10, 13);
        var bars = Session(day, 49_700, 49_200, 49_750, 49_150).ToList();
        var p = SessionClassifier.Classify("BANKNIFTY", bars).Single();
        Assert.Null(p.GapPct);
        Assert.Equal(SessionPath.Unknown, p.Path);
    }

    [Fact]
    public void GapFilled_isTrueOnlyWhenPriceTradesBackThroughThePreviousClose()
    {
        // Gap down to 49,700; the low never returns to 50,000.
        Assert.False(Classify(open: 49_700, close: 49_200, high: 49_750, low: 49_150).GapFilled);
        // Gap down to 49,700 but the session trades above 50,000.
        Assert.True(Classify(open: 49_700, close: 50_200, high: 50_250, low: 49_650).GapFilled);
    }

    [Fact]
    public void DayTypeLabel_combinesOpeningAndPath()
    {
        Assert.Equal("GapDown/Continuation",
            Classify(open: 49_700, close: 49_200, high: 49_750, low: 49_150).DayType);
    }
}
