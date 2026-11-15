using Trading.Domain.Entities;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// VWAP and volume are withheld (null) when the futures volume series falls: Groww futures volume is a running total,
/// so a fall means a mixed or corrupt feed (SENSEX_FUT_1m on 2026-10-09: 6020 then 760, mean step 1248 vs median 80).
/// Missing minutes are NOT a reason to withhold: Groww omits minutes with no trades on the thin SENSEX future, and the
/// bar after a gap carries a normal volume step (checked 2026-10-12), so the differenced volume stays correct.
/// </summary>
public class FuturesVwapQualityTests
{
    private static readonly DateTime Open = new(2026, 10, 12, 3, 45, 0, DateTimeKind.Utc);

    private static Candle Bar(int minute, long volume, decimal price = 100m)
        => new()
        {
            CandleTime = Open.AddMinutes(minute),
            Open = price, High = price + 1, Low = price - 1, Close = price,
            Volume = volume
        };

    private static List<Candle> Bars(IEnumerable<int> minutes)
        => minutes.Select(m => Bar(m, 100 + m * 10L)).ToList();   // running total that never falls

    [Fact]
    public void From_FallingVolumeSeries_ReturnsNull()
    {
        var bars = new List<Candle> { Bar(0, 5000), Bar(1, 6020), Bar(2, 760), Bar(3, 1060) };

        Assert.Null(FuturesFlow.From(bars, 100m));
    }

    [Fact]
    public void From_CompleteRunningTotal_StillGivesVwap()
    {
        var flow = FuturesFlow.From(Bars([0, 1, 2, 3, 4, 5]), 100m);

        Assert.NotNull(flow);
        Assert.True(flow!.Value.CumulativeSource);
    }

    [Fact]
    public void From_MinutesWithNoTrades_StillGivesVwap()
        => Assert.NotNull(FuturesFlow.From(Bars([0, 1, 2, 9, 10, 30]), 100m));
}
