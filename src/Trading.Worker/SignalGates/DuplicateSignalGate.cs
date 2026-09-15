using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

public class DuplicateSignalGate : IPostSignalGate
{
    public async Task<string?> BlockAsync(
      SignalContext ctx,
      MarketSnapshot market,
      Signal signal,
      CancellationToken ct)
    {
        // A rejected entry leaves Status=ORDER_REJECTED and ExitTime=null, so it is invisible to
        // the closed-signal cooldown below. Without its own cooldown the identical setup re-fires
        // on the very next cycle, and since ORDER_REJECTED is not an "open" status OnePositionGate
        // does not hold it either — on 2026-08-27 that produced 10 signals in 13 minutes against
        // an account that could not fund any of them, stopping only when MaxTradesPerDay hit 10.
        //
        // Deliberately NOT gated by IsSettingActive: this is duplicate-order protection, not an
        // entry-condition threshold, and it must fail SAFE. IsSettingActive now reads "no row at
        // all" as "does not apply" (see StrategySettingsService), which is correct for entry
        // conditions that should not silently enforce an un-configured symbol's hardcoded number
        // — but it is the wrong read here: a symbol with no row yet must still get the compiled
        // 5-minute cooldown, not zero. GetInt already gives that for free (no row, or an
        // explicitly deactivated row, both fall back to the caller's default) — an ops override
        // to actually disable this is `SettingValue = 0` with the row active, not IsActive = 0.
        var rejectedCooldownMinutes = ctx.StrategySettings.GetInt("RejectedEntryCooldownMinutes", ctx.Symbol, 5);

        if (rejectedCooldownMinutes > 0)
        {
            var since = IstClock.Now.AddMinutes(-rejectedCooldownMinutes);

            var recentlyRejected =
                (await ctx.SignalRepo.FindAsync(
                    x => x.Symbol == signal.Symbol &&
                         x.SignalType == signal.SignalType &&
                         x.CreatedOn >= since &&
                         (x.Status == SignalStatus.OrderRejected ||
                          x.Status == SignalStatus.OrderUnknown)))
                .OrderByDescending(x => x.CreatedOn)
                .FirstOrDefault();

            if (recentlyRejected != null)
            {
                return $"Cooldown active — {signal.Symbol} {signal.SignalType} entry was "
                     + $"{recentlyRejected.Status} at {recentlyRejected.CreatedOn:HH:mm:ss} "
                     + $"(retry after {rejectedCooldownMinutes}m)";
            }
        }

        var lastClosedSignal =
            (await ctx.SignalRepo.FindAsync(
                x => x.Symbol == signal.Symbol &&
                     x.SignalType == signal.SignalType &&
                     x.Status == SignalStatus.Closed))
            .OrderByDescending(x => x.ExitTime)
            .FirstOrDefault();

        if (lastClosedSignal != null &&
            lastClosedSignal.ExitTime.HasValue)
        {
            var cooldown = TimeSpan.FromMinutes(5);

            if (IstClock.Now - lastClosedSignal.ExitTime.Value < cooldown)
            {
                return $"Cooldown active after {lastClosedSignal.ExitReason}";
            }
        }

        return null;
    }
}
