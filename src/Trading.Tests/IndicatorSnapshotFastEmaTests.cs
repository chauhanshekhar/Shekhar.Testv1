using Trading.Domain.Entities;
using Trading.SignalEngine.Services;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// IndicatorSnapshot.Ema9 / Ema21 are audit-only columns (added 2026-11-04) so EMA9 > EMA21 can be forward-tested from stored
/// values. MarketSnapshotBuilder fills them from the same EmaService and the same 1m closes as Ema20 / Ema50.
/// </summary>
public class IndicatorSnapshotFastEmaTests
{
    private static readonly decimal[] Closes = Enumerable.Range(0, 60).Select(i => 100m + i * 0.5m).ToArray();

    [Fact]
    public void Ema9_and_Ema21_are_nullable_so_rows_written_before_the_columns_stay_valid()
    {
        var row = new IndicatorSnapshot();
        Assert.Null(row.Ema9);
        Assert.Null(row.Ema21);
    }

    [Fact]
    public void Fast_emas_follow_a_rising_series_above_the_slower_one()
    {
        var ema = new EmaService();
        var ema9 = ema.GetEma(Closes, 9);
        var ema21 = ema.GetEma(Closes, 21);
        var ema50 = ema.GetEma(Closes, 50);

        Assert.True(ema9 > ema21 && ema21 > ema50);
        Assert.True(ema9 < Closes[^1]);
    }

    [Fact]
    public void Ema_is_zero_until_enough_closes_exist()
    {
        var ema = new EmaService();
        Assert.Equal(0m, ema.GetEma(Closes.Take(8).ToArray(), 9));
        Assert.Equal(0m, ema.GetEma(Closes.Take(20).ToArray(), 21));
    }
}
