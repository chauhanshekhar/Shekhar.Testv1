using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The closed-bar rule must compare candle times with "now" on the SAME clock. Candle.CandleTime
/// is UTC: GrowwHistoricalDataService stores Groww's epoch seconds with
/// DateTimeOffset.FromUnixTimeSeconds(..).UtcDateTime. Until 2026-09-30 MarketSnapshotBuilder
/// compared it with IstClock.Now (UTC+5:30), so every bar - including the one still forming -
/// counted as closed, and partial bars fed EMA, RSI, Rsi5m, ATR and the last-candle checks.
/// On that day SENSEX's Rsi5m changed at 13:36:40 and 13:38:23, inside the 13:35-13:40 bar.
/// </summary>
public class ClosedBarClockTests
{
    // A candle time stamped exactly the way the Groww client stores it.
    private static DateTime StoredCandleTime(DateTimeOffset barStart) =>
        DateTimeOffset.FromUnixTimeSeconds(barStart.ToUnixTimeSeconds()).UtcDateTime;

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void FormingBar_IsNotClosed(int barMinutes)
    {
        // Opened 30 seconds ago - still forming for either bar size.
        var forming = StoredCandleTime(DateTimeOffset.UtcNow.AddSeconds(-30));

        Assert.False(MarketSnapshotBuilder.IsClosedNow(forming, barMinutes));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    public void BarWhoseDurationHasElapsed_IsClosed(int barMinutes)
    {
        var closed = StoredCandleTime(DateTimeOffset.UtcNow.AddMinutes(-barMinutes).AddSeconds(-30));

        Assert.True(MarketSnapshotBuilder.IsClosedNow(closed, barMinutes));
    }
}
