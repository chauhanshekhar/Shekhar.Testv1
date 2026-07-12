using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

public class EventCalendarService
{
    // Returns true when the current time falls inside a HIGH-impact blackout window.
    public bool IsBlackedOut(IEnumerable<EconomicEvent> events)
    {
        var now = IstClock.Now;
        return events.Any(e =>
            e.Impact == "HIGH" &&
            now >= e.EventTimeUtc.AddMinutes(-e.BlackoutBefore) &&
            now <= e.EventTimeUtc.AddMinutes(e.BlackoutAfter));
    }

    // Returns the first unprocessed event that fired within lookbackMinutes and has EnableNewsEntry = true.
    public EconomicEvent? GetFireableNewsEvent(IEnumerable<EconomicEvent> events, int lookbackMinutes = 15)
    {
        var now = IstClock.Now;
        return events.FirstOrDefault(e =>
            e.EnableNewsEntry &&
            !e.IsProcessed &&
            e.EventTimeUtc <= now &&
            e.EventTimeUtc >= now.AddMinutes(-lookbackMinutes));
    }
}
