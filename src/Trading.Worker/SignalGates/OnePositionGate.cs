namespace Trading.Worker.SignalGates;

public class OnePositionGate : IPreSignalGate
{
    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var todayStart = TradingConstants.TodayUtcStart();

        var todaySignals = await ctx.SignalRepo.FindAsync(s => s.CreatedOn >= todayStart);
        //var closedIds = (await ctx.JournalRepo.FindAsync(j => j.TradeDate >= todayStart))
        //    .Select(j => j.Remarks)
        //    .Where(r => !string.IsNullOrEmpty(r) && !r.StartsWith("PARTIAL_") && long.TryParse(r, out _))
        //    .ToHashSet();
        var closedIds = (await ctx.JournalRepo.FindAsync(j => j.TradeDate >= todayStart))
    .Where(j => j.SignalId != null)
    .Select(j => j.SignalId!.Value.ToString())
    .ToHashSet();

        return todaySignals.Any(s => !closedIds.Contains(s.Id.ToString()))
            ? $"Open position exists — new signal suppressed ({ctx.Symbol})"
            : null;
    }
}
