namespace Trading.Infrastructure.Brokers.Groww;

/// <summary>
/// Index futures, used ONLY as a volume source. Groww publishes no volume for an index (CASH segment); its near-month
/// future (FNO) does. Prices, indicators and stops stay on the index.
///
/// Trading symbol format verified against Groww's instrument.csv on 2026-10-08: NIFTY26OCTFUT and BANKNIFTY26OCTFUT
/// (NSE, expiry 27 Oct), SENSEX26OCTFUT (BSE, expiry 29 Oct) = {underlying}{yy}{MON}FUT, segment FNO.
/// </summary>
public static class GrowwFutures
{
    private static readonly string[] Months =
        ["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

    /// <summary>Candle series the future's 1m bars are stored under, e.g. BANKNIFTY_FUT_1m.</summary>
    public static string CandleSymbol(string underlying) => $"{underlying}_FUT_1m";

    public static string TradingSymbol(string underlying, int year, int month)
        => $"{underlying}{year % 100:D2}{Months[month - 1]}FUT";

    /// <summary>
    /// The contract to read volume from: this month's, or next month's in the last
    /// <paramref name="rollDaysBeforeMonthEnd"/> days of the month. Monthly expiries fall in the last week and the
    /// volume moves to the next contract before expiry.
    /// </summary>
    public static string NearMonth(string underlying, DateTime todayIst, int rollDaysBeforeMonthEnd)
    {
        var month = ContractMonth(todayIst, rollDaysBeforeMonthEnd);
        return TradingSymbol(underlying, month.Year, month.Month);
    }

    /// <summary>The contract after <see cref="NearMonth"/>: the fallback when the near-month one has already expired.</summary>
    public static string NextAfterNearMonth(string underlying, DateTime todayIst, int rollDaysBeforeMonthEnd)
    {
        var month = ContractMonth(todayIst, rollDaysBeforeMonthEnd).AddMonths(1);
        return TradingSymbol(underlying, month.Year, month.Month);
    }

    private static DateTime ContractMonth(DateTime todayIst, int rollDaysBeforeMonthEnd)
    {
        var month = new DateTime(todayIst.Year, todayIst.Month, 1);
        var lastDayBeforeRoll = DateTime.DaysInMonth(todayIst.Year, todayIst.Month) - Math.Max(0, rollDaysBeforeMonthEnd);
        return todayIst.Day > lastDayBeforeRoll ? month.AddMonths(1) : month;
    }
}
