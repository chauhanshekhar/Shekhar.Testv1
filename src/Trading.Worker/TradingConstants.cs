using Trading.Domain;

namespace Trading.Worker;

public static class TradingConstants
{
    // All time is IST — delegates to IstClock.
    public static DateTime NowIst() => IstClock.Now;
    public static DateTime TodayUtcStart() => IstClock.TodayStart;
}
