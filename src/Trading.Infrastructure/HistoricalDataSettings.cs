namespace Trading.Infrastructure;

public class HistoricalDataSettings
{
    public int StartupBackfillMinutes { get; set; } = 75;

    /// <summary>Seconds after each minute boundary to fetch candles (0-30).</summary>
    public int MinuteOffsetSeconds { get; set; } = 3;

    /// <summary>
    /// Also fetch the near-month index future's 1m candles (FNO, stored as {SYMBOL}_FUT_1m) - the only source of
    /// volume and VWAP, which the index itself does not publish. false = off.
    /// </summary>
    public bool FetchIndexFutures { get; set; } = true;

    /// <summary>Use next month's future this many days before the month ends (see GrowwFutures.NearMonth).</summary>
    public int FuturesRollDaysBeforeMonthEnd { get; set; } = 7;
}
