using Trading.Infrastructure.Brokers.Groww;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Expiry auto-roll: picking the next option expiry from the Groww instrument CSV and deciding when a
/// stored date needs replacing. Fixtures are tiny synthetic CSVs, not real instrument data.
/// </summary>
public class ExpiryRollTests
{
    private static readonly DateTime Today = new(2026, 11, 16);

    private const string Header = "exchange,exchange_token,trading_symbol,underlying_symbol,instrument_type,expiry_date,strike_price";

    private static Task<IReadOnlyDictionary<string, string>> Run(string csv, params (string, string)[] wanted)
        => ExpiryRollService.NearestExpiriesAsync(new StringReader(csv), wanted, Today);

    [Fact]
    public async Task Picks_nearest_future_expiry_per_symbol()
    {
        var csv = string.Join('\n',
            Header,
            "NSE,1,NIFTY-A,NIFTY,CE,2026-11-24,25000",
            "NSE,2,NIFTY-B,NIFTY,PE,2026-11-17,25000",
            "NSE,3,NIFTY-C,NIFTY,CE,2026-12-01,25000",
            "BSE,4,SENSEX-A,SENSEX,CE,2026-11-19,80000");

        var r = await Run(csv, ("NIFTY", "NSE"), ("SENSEX", "BSE"));

        Assert.Equal("2026-11-17", r["NIFTY"]);
        Assert.Equal("2026-11-19", r["SENSEX"]);
    }

    [Fact]
    public async Task Expiry_today_is_kept_as_a_candidate()
    {
        var csv = Header + "\nNSE,1,NIFTY-A,NIFTY,CE,2026-11-16,25000\nNSE,2,NIFTY-B,NIFTY,CE,2026-11-23,25000";

        var r = await Run(csv, ("NIFTY", "NSE"));

        Assert.Equal("2026-11-16", r["NIFTY"]);
    }

    [Fact]
    public async Task Past_expiries_and_non_option_rows_are_ignored()
    {
        var csv = string.Join('\n',
            Header,
            "NSE,1,NIFTY-OLD,NIFTY,CE,2026-11-10,25000",
            "NSE,2,NIFTY-FUT,NIFTY,FUT,2026-11-18,0",
            "NSE,3,NIFTY-OK,NIFTY,PE,2026-11-25,25000");

        var r = await Run(csv, ("NIFTY", "NSE"));

        Assert.Equal("2026-11-25", r["NIFTY"]);
    }

    [Fact]
    public async Task Wrong_exchange_does_not_match()
    {
        var csv = Header + "\nBSE,1,NIFTY-X,NIFTY,CE,2026-11-18,25000";

        var r = await Run(csv, ("NIFTY", "NSE"));

        Assert.False(r.ContainsKey("NIFTY"));
    }

    [Fact]
    public async Task No_match_returns_nothing_rather_than_a_guess()
    {
        var csv = Header + "\nNSE,1,OTHER-A,FINNIFTY,CE,2026-11-18,25000";

        var r = await Run(csv, ("BANKNIFTY", "NSE"));

        Assert.Empty(r);
    }

    [Fact]
    public async Task Missing_required_column_returns_nothing()
    {
        var csv = "exchange,underlying_symbol,instrument_type\nNSE,NIFTY,CE";

        var r = await Run(csv, ("NIFTY", "NSE"));

        Assert.Empty(r);
    }

    [Fact]
    public async Task Columns_are_found_by_name_not_position()
    {
        var csv = "expiry_date,instrument_type,underlying_symbol,exchange\n2026-11-18,CE,NIFTY,NSE";

        var r = await Run(csv, ("NIFTY", "NSE"));

        Assert.Equal("2026-11-18", r["NIFTY"]);
    }

    [Fact]
    public async Task Quoted_commas_do_not_shift_columns()
    {
        var csv = Header + "\nNSE,1,\"NIFTY,A\",NIFTY,CE,2026-11-18,25000";

        var r = await Run(csv, ("NIFTY", "NSE"));

        Assert.Equal("2026-11-18", r["NIFTY"]);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("not-a-date", true)]
    [InlineData("2026-11-15", true)]    // yesterday
    [InlineData("2026-11-16", false)]   // today: expiry day must keep IsExpiryDay true
    [InlineData("2026-11-23", false)]   // future: never overwritten
    public void NeedsRoll_only_for_missing_or_past_dates(string? stored, bool expected)
        => Assert.Equal(expected, ExpiryRollService.NeedsRoll(stored, Today));

    [Theory]
    [InlineData("2026-11-16", true)]
    [InlineData("2026-12-31", true)]    // 45 days
    [InlineData("2027-01-01", false)]   // 46 days
    [InlineData("2026-11-15", false)]   // past
    [InlineData("garbage", false)]
    public void IsPlausible_bounds_the_candidate(string candidate, bool expected)
        => Assert.Equal(expected, ExpiryRollService.IsPlausible(candidate, Today));
}
