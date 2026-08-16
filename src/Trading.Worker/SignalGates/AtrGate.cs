namespace Trading.Worker.SignalGates;

/// <summary>
/// Universal market-quality gate — blocks signals in low-volatility/sideways markets.
/// Applies to every strategy (Directional, HeroZero, Expiry) since it runs in SignalWorker's
/// PreGates, before StrategyManager ever selects a strategy.
///
/// EnableSignalScoring (default false, same per-symbol StrategySettings flag SignalEngineService
/// reads) changes what "safety floor" means here:
///   false -> unchanged: MinAtrPercent is the hard floor, exactly as before scoring existed.
///   true  -> MinAtrPercent becomes just the top-quality band for SignalEngineService's AtrScore;
///            this gate instead enforces the much lower MinAtrPercentAbsolute, so genuinely
///            low-but-tradable ATR setups reach the scorer instead of being killed before
///            StrategyManager ever runs. MinAtrPercentAbsolute still hard-blocks below it —
///            volatility protection is never fully removed, only the bar it enforces changes.
/// </summary>
public class AtrGate : IPreSignalGate
{
    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var enableSignalScoring = ctx.StrategySettings.GetBool("EnableSignalScoring", ctx.Symbol, false);

        if (!enableSignalScoring)
        {
            var minAtr = ctx.StrategySettings.GetDecimal("MinAtrPercent", ctx.Symbol, 0.10m);
            return Task.FromResult(market.AtrPct < minAtr
                ? $"ATR {market.AtrPct:F4}% < minimum {minAtr:F4}% ({ctx.Symbol})"
                : null);
        }

        // Scored mode: absolute safety floor only. Defaults to half of the score's own "low"
        // band (see SignalEngineService.AtrScoreLowThreshold) so the gate stays strictly below
        // anything the scorer would ever award points for — a genuine "too low to trade at all"
        // line, not a quality judgment.
        var minAtrForSymbol = ctx.StrategySettings.GetDecimal("MinAtrPercent", ctx.Symbol, 0.10m);
        var atrScoreLowThreshold = ctx.StrategySettings.GetDecimal(
            "AtrScoreLowThreshold", ctx.Symbol, Math.Max(0m, minAtrForSymbol - 0.005m));
        var minAtrAbsolute = ctx.StrategySettings.GetDecimal(
            "MinAtrPercentAbsolute", ctx.Symbol, Math.Max(0m, atrScoreLowThreshold / 2m));

        return Task.FromResult(market.AtrPct < minAtrAbsolute
            ? $"ATR {market.AtrPct:F4}% < absolute minimum {minAtrAbsolute:F4}% ({ctx.Symbol}, scored mode)"
            : null);
    }
}
