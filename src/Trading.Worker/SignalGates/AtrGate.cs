namespace Trading.Worker.SignalGates;

/// <summary>
/// Universal market-quality gate — blocks signals in low-volatility/sideways markets.
/// Applies to every strategy (Directional, HeroZero, Expiry) since it runs in SignalWorker's
/// PreGates, before StrategyManager ever selects a strategy.
/// </summary>
public class AtrGate : IPreSignalGate
{
    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var minAtr = ctx.StrategySettings.GetDecimal("MinAtrPercent", ctx.Symbol, 0.10m);

        if (market.AtrPct < minAtr)
            return Task.FromResult<string?>(
                $"ATR {market.AtrPct:F4}% < minimum {minAtr:F4}% ({ctx.Symbol})");

        return Task.FromResult<string?>(null);
    }
}
