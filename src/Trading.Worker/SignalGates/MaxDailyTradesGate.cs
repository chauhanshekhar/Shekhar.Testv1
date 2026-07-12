using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Blocks new signals once today's completed trade count reaches MaxTradesPerDay.
/// ORDER_REJECTED signals are excluded — a rejected order is not a trade.
/// </summary>
public class MaxDailyTradesGate : IPreSignalGate
{
    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var max = ctx.Settings.MaxTradesPerDay;
        if (max <= 0) return null;   // 0 = unlimited

        var todayStart = IstClock.TodayStart;

        var todayCount = await ctx.SignalRepo.CountAsync(
            s => s.CreatedOn >= todayStart
              && s.Status != SignalStatus.OrderRejected);

        return todayCount >= max
            ? $"Max daily trades reached ({todayCount}/{max}) — no new signals until tomorrow"
            : null;
    }
}
