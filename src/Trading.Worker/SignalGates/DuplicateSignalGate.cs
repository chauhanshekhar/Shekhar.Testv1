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
            var cooldown =
                lastClosedSignal.ExitReason == TradingConst.ExitReason.OptionSlHit
                    ? TimeSpan.FromMinutes(5)
                    : TimeSpan.FromMinutes(15);

            if (IstClock.Now - lastClosedSignal.ExitTime.Value < cooldown)
            {
                return $"Cooldown active after {lastClosedSignal.ExitReason}";
            }
        }

        return null;
    }
}
