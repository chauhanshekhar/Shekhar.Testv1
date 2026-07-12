namespace Trading.Worker.SignalGates;

public class ConsecutiveLossGate : IPreSignalGate
{
    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var todayStart = TradingConstants.TodayUtcStart();
        var maxLosses = ctx.Settings.MaxConsecLosses;

        var recent = (await ctx.JournalRepo.FindAsync(j => j.TradeDate >= todayStart))
            .Where(j => !j.Remarks.StartsWith("PARTIAL_"))
            .OrderByDescending(j => j.TradeDate)
            .Take(maxLosses)
            .ToList();

        return recent.Count >= maxLosses && recent.All(j => j.ProfitLoss < 0)
            ? $"Circuit breaker: {maxLosses} consecutive losses — no more trades today ({ctx.Symbol})"
            : null;
    }
}
