using Trading.Domain.Entities;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Volume and VWAP come from the near-month index future (the index publishes no volume). Contract names were checked
/// against Groww's instrument.csv on 2026-10-08; the cumulative-volume sample is the real BANKNIFTY26OCTFUT response for
/// 2026-10-08 09:15-09:21.
/// </summary>
public class FuturesVolumeTests
{
    // ── Contract names ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("BANKNIFTY", 2026, 10, "BANKNIFTY26OCTFUT")]
    [InlineData("NIFTY", 2026, 10, "NIFTY26OCTFUT")]
    [InlineData("SENSEX", 2026, 10, "SENSEX26OCTFUT")]
    [InlineData("NIFTY", 2027, 1, "NIFTY27JANFUT")]
    public void TradingSymbol_MatchesGrowwInstrumentList(string underlying, int year, int month, string expected)
        => Assert.Equal(expected, GrowwFutures.TradingSymbol(underlying, year, month));

    [Theory]
    [InlineData("2026-10-08", "NIFTY26OCTFUT")]   // ordinary day
    [InlineData("2026-10-24", "NIFTY26OCTFUT")]   // 31 - 7 = 24: last day on the October contract
    [InlineData("2026-10-25", "NIFTY26NOVFUT")]   // roll window
    [InlineData("2026-11-24", "NIFTY26DECFUT")]   // November has 30 days: rolls after the 23rd
    [InlineData("2026-12-26", "NIFTY27JANFUT")]   // year boundary
    public void NearMonth_RollsInTheLastWeek(string today, string expected)
        => Assert.Equal(expected, GrowwFutures.NearMonth("NIFTY", DateTime.Parse(today), 7));

    [Fact]
    public void NextAfterNearMonth_IsTheFollowingContract()
    {
        Assert.Equal("BANKNIFTY26NOVFUT", GrowwFutures.NextAfterNearMonth("BANKNIFTY", new DateTime(2026, 10, 8), 7));
        Assert.Equal("BANKNIFTY27JANFUT", GrowwFutures.NextAfterNearMonth("BANKNIFTY", new DateTime(2026, 11, 28), 7));
    }

    [Fact]
    public void CandleSymbol_IsSeparateFromTheIndexSeries()
        => Assert.Equal("SENSEX_FUT_1m", GrowwFutures.CandleSymbol("SENSEX"));

    // ── Cumulative -> per-bar volume ────────────────────────────────────────────────────────────

    private static readonly long[] Sample = [17760, 21480, 25680, 28200, 32460, 35790, 39120];

    [Fact]
    public void PerBarVolumes_RealCumulativeSample_IsDifferenced()
    {
        var (volumes, cumulative) = FuturesFlow.PerBarVolumes(Sample);

        Assert.True(cumulative);
        Assert.Equal([17760L, 3720, 4200, 2520, 4260, 3330, 3330], volumes);
        Assert.All(volumes.Skip(1), v => Assert.Equal(0, v % 30));     // whole BANKNIFTY lots
    }

    [Fact]
    public void PerBarVolumes_SeriesThatFalls_IsAlreadyPerBar()
    {
        var (volumes, cumulative) = FuturesFlow.PerBarVolumes([3720, 4200, 2520, 4260]);

        Assert.False(cumulative);
        Assert.Equal([3720L, 4200, 2520, 4260], volumes);
    }

    // ── VWAP in index points ─────────────────────────────────────────────────────────────────

    private static Candle Bar(DateTime utc, decimal high, decimal low, decimal close, long volume)
        => new() { CandleTime = utc, Open = close, High = high, Low = low, Close = close, Volume = volume };

    [Fact]
    public void Vwap_IsShiftedByTheBasis_SoItComparesWithTheIndex()
    {
        // Typical prices 100 (volume 1) and 110 (volume 3): future VWAP 107.5. Future close 112, index 100 -> basis 12.
        var t = new DateTime(2026, 10, 8, 3, 45, 0, DateTimeKind.Utc);
        var bars = new List<Candle> { Bar(t, 100, 100, 100, 1), Bar(t.AddMinutes(1), 110, 110, 110, 4) };
        bars[1].Close = 112m;
        bars[1].High = 113m;
        bars[1].Low = 105m;                                              // typical (113 + 105 + 112) / 3 = 110

        var (volumes, _) = FuturesFlow.PerBarVolumes(bars.Select(b => b.Volume).ToList());
        var vwap = FuturesFlow.VwapInIndexPoints(bars, volumes, indexLtp: 100m);

        Assert.Equal(107.5m - 12m, vwap);
        Assert.True(100m > vwap);                                        // index above <=> future 112 above its VWAP 107.5
    }

    [Fact]
    public void Vwap_WithoutVolume_IsNull()
    {
        var t = new DateTime(2026, 10, 8, 3, 45, 0, DateTimeKind.Utc);
        var bars = new List<Candle> { Bar(t, 100, 100, 100, 0) };

        Assert.Null(FuturesFlow.VwapInIndexPoints(bars, [0], 100m));
        Assert.Null(FuturesFlow.From(bars, 100m));
    }

    // ── 5-minute volume ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void FiveMinuteVolume_UsesOnlyCompleteBuckets()
    {
        // 09:15-09:19 = 5 x 100, 09:20-09:24 = 5 x 300, 09:25-09:26 = 2 bars (incomplete, ignored).
        var t = new DateTime(2026, 10, 8, 3, 45, 0, DateTimeKind.Utc);
        var bars = Enumerable.Range(0, 12).Select(i => Bar(t.AddMinutes(i), 1, 1, 1, 0)).ToList();
        var volumes = Enumerable.Range(0, 12).Select(i => i < 5 ? 100L : 300L).ToArray();

        var five = FuturesFlow.FiveMinuteVolume(bars, volumes);

        Assert.Equal((1500m, 500m), five);
    }

    [Fact]
    public void RealSample_GivesVwapButNotYetA5mComparison()
    {
        // 09:15-09:21: one complete 5m bucket only - VWAP is available, the 5m volume comparison is not (0 / 1 as today).
        var t = new DateTime(2026, 10, 8, 3, 45, 0, DateTimeKind.Utc);
        decimal[][] ohlc =
        [
            [55288.0m, 55035.2m, 55080.0m], [55125.0m, 55047.8m, 55125.0m], [55111.8m, 55074.8m, 55079.6m],
            [55092.2m, 55070.0m, 55082.8m], [55079.0m, 55047.8m, 55047.8m], [55087.8m, 55020.0m, 55087.8m],
            [55105.0m, 55043.8m, 55087.8m],
        ];
        var bars = ohlc.Select((x, i) => Bar(t.AddMinutes(i), x[0], x[1], x[2], Sample[i])).ToList();

        var flow = FuturesFlow.From(bars, indexLtp: 54900m);

        Assert.NotNull(flow);
        Assert.True(flow!.Value.CumulativeSource);
        Assert.Equal(0m, flow.Value.CurrentVolume);
        Assert.Equal(1m, flow.Value.AvgVolume20);
        Assert.InRange(flow.Value.Vwap, 54700m, 54950m);                 // within a few points of the index, not the future
    }
}
