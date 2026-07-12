namespace Trading.Worker.SignalGates;

public class CircuitBreakerGate : IPreSignalGate
{
    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var s = ctx.Settings;
        var maxLossPerDay = s.Capital * s.MaxLossPercent / 100m;
        var todayStart = TradingConstants.TodayUtcStart();

        var pnl = await ctx.PnlRepo.FirstOrDefaultAsync(p => p.TradeDate >= todayStart);
        if (pnl == null) return null;

        if (pnl.TotalTrades >= s.MaxTradesPerDay)
            return $"Circuit breaker: max trades/day {s.MaxTradesPerDay} reached ({ctx.Symbol})";

        if (pnl.TotalLoss >= maxLossPerDay)
            return $"Circuit breaker: daily loss ₹{pnl.TotalLoss:F0} >= ₹{maxLossPerDay:F0} limit ({ctx.Symbol})";

        // Realized + unrealized (open-position MTM) — blocks new entries while an
        // existing loss-making position would already breach the daily limit if closed now.
        var combinedLoss = -(pnl.NetProfit + pnl.UnrealizedPnl);
        if (combinedLoss >= maxLossPerDay)
            return $"Circuit breaker: realized+unrealized loss ₹{combinedLoss:F0} >= ₹{maxLossPerDay:F0} limit ({ctx.Symbol})";

        return null;
    }
}
