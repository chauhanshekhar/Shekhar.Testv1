namespace Trading.Domain;

/// <summary>
/// In-memory cache of exchange holidays loaded at startup by HolidayCalendarWorker.
/// Thread-safe: set is replaced atomically, never mutated in place.
/// </summary>
public static class HolidayCache
{
    private static volatile HashSet<DateTime> _holidays = new();

    public static void Set(IEnumerable<DateTime> dates)
        => _holidays = new HashSet<DateTime>(dates.Select(d => d.Date));

    public static bool IsHolidayToday() => IsHoliday(IstClock.Today);

    public static bool IsHoliday(DateTime date) => _holidays.Contains(date.Date);
}

/// <summary>
/// Single source of truth for current Indian Standard Time (IST = UTC+5:30).
/// Use IstClock.Now everywhere instead of DateTime.UtcNow or DateTime.Now.
/// All datetime values stored in DB are IST.
/// </summary>
public static class IstClock
{
    private static readonly TimeZoneInfo Ist =
        TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");

    public static DateTime Now =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Ist);

    public static DateTime Today => Now.Date;
    public static DateTime TodayStart => Now.Date;
    public static DateTime TodayEnd => Now.Date.AddDays(1).AddTicks(-1);
}

/// <summary>
/// NSE/BSE market hours in IST. Single source of truth.
/// </summary>
public static class MarketHours
{
    public static readonly TimeSpan SessionOpen  = new(9, 15, 0);
    public static readonly TimeSpan SessionClose = new(15, 30, 0);
    public static readonly TimeSpan SignalOpen   = new(9, 25, 0);

    // These three are ordered around BrokerIntradayCutoff and must stay that way:
    // enter -> protect -> force exit, all COMPLETED before the broker stops accepting
    // intraday orders. Order history shows no order has ever been accepted at or after
    // 15:20 (Groww: "intraday orders are not available as market about to close"), so a
    // ForceExit later than the cutoff cannot fill, and a SignalClose later than it produces
    // entries that either reject outright or fill with no placeable stop behind them.
    public static readonly TimeSpan SignalClose  = new(15, 10, 0);
    public static readonly TimeSpan ForceExit    = new(15, 15, 0);

    /// <summary>
    /// Time after which the broker stops accepting fresh intraday (MIS) orders as it begins its
    /// own square-off. Past this point an SL_M placement is guaranteed to be rejected, so the
    /// exit path must stop attempting one and rely on the polling MARKET fallback instead.
    /// </summary>
    public static readonly TimeSpan BrokerIntradayCutoff = new(15, 20, 0);

    public static bool IsOpen()
    {
#if TESTING
        return true;
#else
        var now = IstClock.Now;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        if (HolidayCache.IsHolidayToday()) return false;
        var t = now.TimeOfDay;
        return t >= SessionOpen && t <= SessionClose;
#endif
    }

    public static bool IsSignalWindow()
    {
#if TESTING
        return true;
#else
        var now = IstClock.Now;
        if (now.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return false;
        if (HolidayCache.IsHolidayToday()) return false;
        var t = now.TimeOfDay;
        return t >= SignalOpen && t <= SignalClose;
#endif
    }

    public static bool IsForceExitWindow() => IstClock.Now.TimeOfDay >= ForceExit;

    /// <summary>True once the broker has stopped accepting fresh intraday orders.</summary>
    public static bool IsPastBrokerIntradayCutoff() => IstClock.Now.TimeOfDay >= BrokerIntradayCutoff;

    /// <summary>
    /// Most recent trading session before <paramref name="fromDate"/> — skips weekends
    /// and exchange holidays. Falls back to a 10-calendar-day lookback if no trading day
    /// is found (should never happen in practice).
    /// </summary>
    public static DateTime PreviousTradingSession(DateTime fromDate)
    {
        var day = fromDate.Date.AddDays(-1);

        for (var i = 0; i < 10; i++)
        {
            if (day.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
                && !HolidayCache.IsHoliday(day))
                return day;

            day = day.AddDays(-1);
        }

        return day;
    }
}
