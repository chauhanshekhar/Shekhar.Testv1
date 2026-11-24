using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

/// <summary>
/// Blocks new signals once today's completed trade count reaches MaxTradesPerDay.
/// ORDER_REJECTED signals are excluded — a rejected order is not a trade.
///
/// Re-registered in SignalWorker.PreGates behind a switch so registering it changes nothing until enabled:
///   EnableMaxDailyTradesGate  0/1  (default 0)
///   MaxTradesPerDay           int  (StrategySettings; default TradingSettings.MaxTradesPerDay = 10; 0 = unlimited)
/// The count is across ALL symbols for the day (unchanged from the original gate).
/// </summary>
public class MaxDailyTradesGate : IPreSignalGate
{
    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        if (!ctx.StrategySettings.GetBool("EnableMaxDailyTradesGate", ctx.Symbol, false))
            return null;

        var max = ctx.StrategySettings.GetInt("MaxTradesPerDay", ctx.Symbol, ctx.Settings.MaxTradesPerDay);
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
