using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Blocks new signals during HIGH-impact economic event blackout windows.
/// Returns a descriptive reason (event name + window) so the block appears in logs and alerts.
/// </summary>
public class EventBlackoutGate : IPreSignalGate
{
    public async Task<string?> BlockAsync(
        SignalContext ctx,
        MarketSnapshot market,
        CancellationToken ct)
    {
        var nowUtc = DateTime.UtcNow;

        var upcomingEvents =
            await ctx.EventRepo.FindAsync(
                e => e.EventTimeUtc >= nowUtc.AddHours(-2)
                  && e.EventTimeUtc <= nowUtc.AddHours(1));


        var blockedBy =
            FindBlackoutEvent(upcomingEvents);


        if (blockedBy == null)
            return null;


        var eventIst =
            blockedBy.EventTimeUtc
                .AddHours(5)
                .AddMinutes(30);


        var windowEnd =
            eventIst.AddMinutes(
                blockedBy.BlackoutAfter);


        return
            $"Event blackout: {blockedBy.Name} " +
            $"event {eventIst:HH:mm} IST " +
            $"ends {windowEnd:HH:mm}";
    }

    private static EconomicEvent? FindBlackoutEvent(
        IEnumerable<EconomicEvent> events)
    {
        var nowUtc = DateTime.UtcNow;

        return events.FirstOrDefault(e =>
            e.Impact.Equals(
                "HIGH",
                StringComparison.OrdinalIgnoreCase)
            &&
            nowUtc >= e.EventTimeUtc.AddMinutes(-e.BlackoutBefore)
            &&
            nowUtc <= e.EventTimeUtc.AddMinutes(e.BlackoutAfter));
    }
}
