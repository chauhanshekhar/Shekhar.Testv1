using Trading.Domain;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The live session-trend gate: enter only when the session so far is travelling in the trade's
/// direction. Geometry: ATR 20, so 0.75 ATR = 15 points. Previous close 50,000 throughout, with the
/// day open moved to produce the gap under test.
/// </summary>
public class SessionTrendGateTests
{
    private const decimal Atr = 20m, PrevClose = 50_000m;

    private static string? Put(
        decimal ltp, decimal dayOpen, decimal dayHigh, decimal dayLow,
        decimal minMove = 0.75m, decimal minEff = 0.50m, decimal nearExtreme = 0m,
        int opening = SessionTrendGate.OpeningAny, int side = SessionTrendGate.SideBoth,
        decimal prevClose = PrevClose) =>
        SessionTrendGate.Evaluate(TradingConst.SignalType.PUT, ltp, dayOpen, dayHigh, dayLow, Atr,
            prevClose, minMove, minEff, nearExtreme, opening, gapPct: 0.30m, side: side);

    [Fact]
    public void ASessionGoingDownCleanly_isAllowed()
    {
        // 40 points below the open (2 ATR) on a 45-point range => efficiency 0.89.
        Assert.Null(Put(ltp: 49_460, dayOpen: 49_500, dayHigh: 49_505, dayLow: 49_460));
    }

    [Fact]
    public void ASessionThatHasBarelyMoved_isBlocked()
    {
        var r = Put(ltp: 49_490, dayOpen: 49_500, dayHigh: 49_505, dayLow: 49_480);
        Assert.NotNull(r);
        Assert.Contains("needs 0.75", r);
    }

    [Fact]
    public void ARoundTrippingSession_isBlocked_evenAfterTravelling()
    {
        // 20 points below the open (1 ATR, enough) but a 200-point range => efficiency 0.10.
        var r = Put(ltp: 49_480, dayOpen: 49_500, dayHigh: 49_600, dayLow: 49_400);
        Assert.NotNull(r);
        Assert.Contains("round-tripping", r);
    }

    [Fact]
    public void AnUpwardSession_isBlockedForAPut()
    {
        var r = Put(ltp: 49_540, dayOpen: 49_500, dayHigh: 49_545, dayLow: 49_500);
        Assert.NotNull(r);
        Assert.Contains("moved -2.00 ATR down", r);
    }

    [Fact]
    public void NearExtreme_blocksAStalledMove_andIsOffByDefault()
    {
        // Price 40 points (2 ATR) above the session low, after a clean 2.5 ATR fall.
        const decimal ltp = 49_450, open = 49_500, high = 49_505, low = 49_410;

        Assert.NotNull(Put(ltp, open, high, low, nearExtreme: 1.0m));
        Assert.Contains("stalled", Put(ltp, open, high, low, nearExtreme: 1.0m)!);
        Assert.Null(Put(ltp, open, high, low));          // default 0 = test disabled
    }

    [Fact]
    public void ScopedToGapDown_itIgnoresOtherOpenings()
    {
        // A session that would be blocked, but opened flat (49,990 vs 50,000 = -0.02%).
        Assert.Null(Put(ltp: 49_985, dayOpen: 49_990, dayHigh: 49_995, dayLow: 49_980,
            opening: SessionTrendGate.OpeningGapDown));

        // The same shape on a gap-down open (49,500 vs 50,000 = -1.0%) is in scope and blocked.
        Assert.NotNull(Put(ltp: 49_495, dayOpen: 49_500, dayHigh: 49_505, dayLow: 49_490,
            opening: SessionTrendGate.OpeningGapDown));
    }

    [Fact]
    public void SideCallOnly_leavesPutsAlone()
    {
        Assert.Null(Put(ltp: 49_540, dayOpen: 49_500, dayHigh: 49_545, dayLow: 49_500,
            side: SessionTrendGate.SideCallOnly));
    }

    [Fact]
    public void MissingInputs_failOpen()
    {
        Assert.Null(SessionTrendGate.Evaluate(TradingConst.SignalType.PUT,
            ltp: 49_460, dayOpen: 49_500, dayHigh: 49_505, dayLow: 49_460, atrRaw: 0m,
            yesterdayClose: PrevClose, 0.75m, 0.50m, 0m, SessionTrendGate.OpeningAny, 0.30m, SessionTrendGate.SideBoth));

        Assert.Null(SessionTrendGate.Evaluate(TradingConst.SignalType.PUT,
            ltp: 49_460, dayOpen: 0m, dayHigh: 49_505, dayLow: 49_460, atrRaw: Atr,
            yesterdayClose: PrevClose, 0.75m, 0.50m, 0m, SessionTrendGate.OpeningAny, 0.30m, SessionTrendGate.SideBoth));
    }

    [Fact]
    public void ACallOnAnUpwardSession_isAllowed()
    {
        Assert.Null(SessionTrendGate.Evaluate(TradingConst.SignalType.CALL,
            ltp: 50_540, dayOpen: 50_500, dayHigh: 50_545, dayLow: 50_500, atrRaw: Atr,
            yesterdayClose: PrevClose, 0.75m, 0.50m, 0m, SessionTrendGate.OpeningAny, 0.30m, SessionTrendGate.SideBoth));
    }

    [Fact]
    public void ClassifyOpening_usesThePreviousClose()
    {
        Assert.Equal(SessionTrendGate.OpeningGapUp, SessionTrendGate.ClassifyOpening(50_200m, 50_000m, 0.30m));
        Assert.Equal(SessionTrendGate.OpeningGapDown, SessionTrendGate.ClassifyOpening(49_800m, 50_000m, 0.30m));
        Assert.Equal(SessionTrendGate.OpeningNormal, SessionTrendGate.ClassifyOpening(50_050m, 50_000m, 0.30m));
        Assert.Null(SessionTrendGate.ClassifyOpening(50_050m, 0m, 0.30m));
    }
}
