namespace Trading.Backtest.Data;

/// <summary>One closed bar. <see cref="OpenUtc"/> is the bar's open time in UTC (the Candles table stores UTC).</summary>
public readonly record struct Bar(DateTime OpenUtc, double Open, double High, double Low, double Close);

/// <summary>
/// Everything the live engine could have known at one decision minute, built from bars that were already CLOSED at that
/// minute. <see cref="TimeUtc"/> is the minute boundary: a bar opened at or after it is not visible here.
/// </summary>
public sealed record MarketPoint
{
    public required string Symbol { get; init; }
    public required DateTime TimeUtc { get; init; }
    /// <summary>Index of the first 1m bar that opens at or after <see cref="TimeUtc"/> (the entry path starts here).</summary>
    public required int NextBarIndex { get; init; }
    public required double Ltp { get; init; }
    public required double Ema20 { get; init; }
    public required double Ema50 { get; init; }
    public required double Rsi1m { get; init; }
    /// <summary>5-minute RSI(14); null until 15 closed 5m bars exist (the live conditions then fail closed).</summary>
    public required double? Rsi5m { get; init; }
    public required double Atr { get; init; }
    public required double AtrPct { get; init; }
    public required double DayChangePct { get; init; }
    public required double DayOpen { get; init; }
    public required double DayLow { get; init; }
    public required double DayHigh { get; init; }
    /// <summary>5m EMAs from the last 100 closed 5m bars; 0 when fewer than 20/50 bars exist (the early-trend gate then lets a signal through).</summary>
    public required double Ema20_5m { get; init; }
    public required double Ema50_5m { get; init; }
    /// <summary>Research only: 1m EMA9 / EMA21 (not stored in IndicatorSnapshots, rebuilt from candles).</summary>
    /// <summary>Research: Wilder ATR(14) of the closed 5m bars (0 until 15 exist).</summary>
    public double Atr5m { get; init; }
    public double Ema9 { get; init; }
    public double Ema21 { get; init; }
    public required Bar LastClosedBar { get; init; }
    /// <summary>Open of the session's first 1m bar (not the window-drifting DayOpen) and the lowest low since then.</summary>
    public double SessionOpen { get; init; }
    public double SessionLow { get; init; }
    /// <summary>Lowest low of the last 10 closed 1m bars (pullback test).</summary>
    public double MinLow10 { get; init; }
    /// <summary>Highest high of the 10 closed 1m bars before the last one (breakout test).</summary>
    public double MaxHigh10Prior { get; init; }

    /// <summary>High / low of the last CLOSED 5m bar (what production reads as LastClosed5mCandle); 0 until one exists.</summary>
    public double Prev5mHigh { get; init; }
    public double Prev5mLow { get; init; }

    /// <summary>Opening gap: (first bar open of today - last close of the previous session) / previous close x 100; null on the first session in the data.</summary>
    public double? OpenGapPct { get; init; }

    // ── from the real MarketRegimeService / MarketClassificationService, run on the rebuilt inputs (RegimeEnricher); null until enriched ──
    public string? RegimeTrend { get; init; }
    public string? RegimeVolatility { get; init; }
    public string? ReplayedMarketState { get; init; }

    /// <summary>
    /// Closed 1m decision minutes since EMA20 first rose above EMA50 (0 = the first minute it is above). Null when EMA20 is not above
    /// EMA50, or when the run was already under way at the first point of the session (its start is unknown, so it is never "early").
    /// Set by RegimeEnricher; research only.
    /// </summary>
    public int? BarsSinceEma1mUp { get; init; }

    // ── joined from IndicatorSnapshots (last snapshot at or before TimeUtc + IST offset); null when none exists ─────────
    public double? Pcr { get; init; }
    public string? MarketState { get; init; }
    public string? TrendDirection { get; init; }
    public string? StrategySelected { get; init; }
    public string? StoredMarketRegime { get; init; }

    public DateTime TimeIst => TimeUtc + SessionClock.IstOffset;
}

public static class SessionClock
{
    public static readonly TimeSpan IstOffset = TimeSpan.FromHours(5) + TimeSpan.FromMinutes(30);

    /// <summary>Entry window used by the validated research scripts: 09:20 IST to 15:15 IST (exclusive), expressed in UTC.</summary>
    public static readonly TimeSpan EntryStartUtc = new(3, 50, 0);
    public static readonly TimeSpan EntryEndUtc = new(9, 45, 0);

    /// <summary>Minutes after IST midnight for a UTC instant.</summary>
    public static int MinuteOfDayIst(DateTime utc)
    {
        var ist = utc + IstOffset;
        return ist.Hour * 60 + ist.Minute;
    }
}

/// <summary>Last IndicatorSnapshots row of a minute (one row per symbol-minute, latest second wins).</summary>
public sealed class SnapshotRow
{
    public string Symbol { get; set; } = "";
    public DateTime SnapshotTime { get; set; }
    public decimal Pcr { get; set; }
    public string? MarketState { get; set; }
    public string? TrendDirection { get; set; }
    public string? StrategySelected { get; set; }
    public string? MarketRegime { get; set; }
}

/// <summary>One OptionMinuteHistory row, trimmed to what the replay uses.</summary>
public sealed class OptionMinuteRow
{
    public string Symbol { get; set; } = "";
    public DateTime MinuteTime { get; set; }
    public string TradingSymbol { get; set; } = "";
    public decimal StrikePrice { get; set; }
    public string OptionType { get; set; } = "";
    public DateTime ExpiryDate { get; set; }
    public decimal Ltp { get; set; }
    public decimal Delta { get; set; }
    public long ChangeInOi { get; set; }
    /// <summary>Open interest of the contract at that minute (the live OI gate differences it between snapshots).</summary>
    public long OpenInterest { get; set; }
    public decimal UnderlyingLtp { get; set; }
}
