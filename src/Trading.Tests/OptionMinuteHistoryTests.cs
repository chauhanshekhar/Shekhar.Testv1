using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Worker;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// OptionMinuteHistory keeps a per-minute copy of the ATM±5 chain that OptionChainWorker otherwise
/// prunes after 2 hours, so backtests can replay real premium and delta instead of assuming
/// premium = 0.5 × index move. The write itself is best-effort DB work; these cover the pure parts.
/// </summary>
public class OptionMinuteHistoryTests
{
    private static OptionChainData Leg(string sym, decimal strike, string type, decimal ltp, decimal delta) => new()
    {
        Symbol = "NIFTY",
        TradingSymbol = sym,
        StrikePrice = strike,
        OptionType = type,
        ExpiryDate = new DateTime(2026, 10, 7),
        Ltp = ltp,
        Delta = delta,
        ImpliedVolatility = 12.5m,
        OpenInterest = 1_000,
        ChangeInOi = 50,
        UnderlyingLtp = 24_010m,
    };

    [Fact]
    public void ToMinute_TruncatesSecondsAndBelow()
    {
        var t = new DateTime(2026, 9, 30, 10, 15, 42, 873);

        Assert.Equal(new DateTime(2026, 9, 30, 10, 15, 0), OptionChainWorker.ToMinute(t));
    }

    [Fact]
    public void ToMinuteHistory_CopiesEveryLegField()
    {
        var minute = new DateTime(2026, 9, 30, 10, 15, 0);
        var now = minute.AddSeconds(12);

        var row = Assert.Single(OptionChainWorker.ToMinuteHistory(
            [Leg("NIFTY26OCT24000CE", 24_000m, TradingConst.OptionType.CE, 120.5m, 0.52m)], minute, now));

        Assert.Equal("NIFTY", row.Symbol);
        Assert.Equal(minute, row.MinuteTime);
        Assert.Equal("NIFTY26OCT24000CE", row.TradingSymbol);
        Assert.Equal(24_000m, row.StrikePrice);
        Assert.Equal(TradingConst.OptionType.CE, row.OptionType);
        Assert.Equal(new DateTime(2026, 10, 7), row.ExpiryDate);
        Assert.Equal(120.5m, row.Ltp);
        Assert.Equal(0.52m, row.Delta);
        Assert.Equal(12.5m, row.ImpliedVolatility);
        Assert.Equal(1_000, row.OpenInterest);
        Assert.Equal(50, row.ChangeInOi);
        Assert.Equal(24_010m, row.UnderlyingLtp);
        Assert.Equal(now, row.CreatedOn);
    }

    [Fact]
    public void ToMinuteHistory_OneRowPerLeg_MatchingTheUniqueIndex()
    {
        // Duplicate legs in one payload must not produce two rows for the same
        // (Symbol, MinuteTime, TradingSymbol) - the insert would violate the unique index.
        var rows = OptionChainWorker.ToMinuteHistory(
        [
            Leg("A_CE", 24_000m, TradingConst.OptionType.CE, 120m, 0.5m),
            Leg("A_CE", 24_000m, TradingConst.OptionType.CE, 121m, 0.5m),
            Leg("A_PE", 24_000m, TradingConst.OptionType.PE, 110m, -0.5m),
        ], DateTime.Today, DateTime.Now);

        Assert.Equal(2, rows.Count);
        Assert.Equal(rows.Count, rows.Select(r => r.TradingSymbol).Distinct().Count());
    }

    [Fact]
    public void ToMinuteHistory_SkipsLegsWithNoPrice()
    {
        var rows = OptionChainWorker.ToMinuteHistory(
        [
            Leg("A_CE", 24_000m, TradingConst.OptionType.CE, 0m, 0.5m),   // no quote
            Leg("", 24_000m, TradingConst.OptionType.PE, 100m, -0.5m),    // no symbol
            Leg("B_CE", 24_050m, TradingConst.OptionType.CE, 95m, 0.45m),
        ], DateTime.Today, DateTime.Now);

        Assert.Equal("B_CE", Assert.Single(rows).TradingSymbol);
    }

    [Fact]
    public void ToMinuteHistory_KeepsPutDeltaSign()
        => Assert.Equal(-0.48m, Assert.Single(OptionChainWorker.ToMinuteHistory(
            [Leg("P", 24_000m, TradingConst.OptionType.PE, 100m, -0.48m)], DateTime.Today, DateTime.Now)).Delta);
}
