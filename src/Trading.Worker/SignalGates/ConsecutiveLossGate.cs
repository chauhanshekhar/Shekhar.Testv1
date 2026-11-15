using Trading.Application.Interfaces;
using Trading.Domain.Entities;

namespace Trading.Worker.SignalGates;

public class ConsecutiveLossGate : IPreSignalGate
{
    public async Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var isPaper = ctx.Orders?.CurrentMode == TradingMode.Paper;
        var maxLosses = ResolveLimit(ctx.StrategySettings, ctx.Symbol, ctx.Settings.MaxConsecLosses, isPaper);
        if (maxLosses <= 0)
            return null;

        var todayStart = TradingConstants.TodayUtcStart();

        var recent = (await ctx.JournalRepo.FindAsync(j => j.TradeDate >= todayStart))
            .Where(j => !j.Remarks.StartsWith("PARTIAL_"))
            .OrderByDescending(j => j.TradeDate)
            .Take(maxLosses)
            .ToList();

        return recent.Count >= maxLosses && recent.All(j => j.ProfitLoss < 0)
            ? $"Circuit breaker: {maxLosses} consecutive losses — no more trades today ({ctx.Symbol})"
            : null;
    }

    /// <summary>
    /// How many consecutive losing trades stop trading for the day; 0 = gate off. StrategySettings,
    /// per symbol, so the gate can be relaxed WITHOUT editing TradeJournal (flipping losses to
    /// profits also blinds CircuitBreakerGate's daily-loss limit and StrategySelectorService's win
    /// rates):
    ///   EnableConsecutiveLossGate      (default on)  — master switch, live and paper
    ///   EnableConsecutiveLossGatePaper (default on)  — in Paper mode the gate also needs this
    ///   MaxConsecutiveLosses           (default TradingSettings.MaxConsecLosses = 3)
    /// Defaults reproduce the previous behaviour exactly: 3, in both modes.
    /// </summary>
    internal static int ResolveLimit(IStrategySettingsService settings, string symbol, int compiledDefault, bool isPaper)
    {
        if (!settings.GetBool("EnableConsecutiveLossGate", symbol, true))
            return 0;
        if (isPaper && !settings.GetBool("EnableConsecutiveLossGatePaper", symbol, true))
            return 0;
        return Math.Max(0, settings.GetInt("MaxConsecutiveLosses", symbol, compiledDefault));
    }
}
