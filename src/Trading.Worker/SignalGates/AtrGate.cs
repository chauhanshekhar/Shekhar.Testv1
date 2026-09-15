namespace Trading.Worker.SignalGates;

/// <summary>
/// Universal market-quality gate — blocks signals in low-volatility markets. Applies to every
/// strategy (Directional, HeroZero, Expiry) since it runs in SignalWorker's PreGates, before
/// StrategyManager ever selects a strategy.
/// </summary>
public class AtrGate : IPreSignalGate
{
    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        // This gate previously read ONLY MinAtrPercent, so it could not be switched off from the
        // database at all - setting EnableAtrFilter = 0 did nothing here and the gate kept firing.
        // It now honours the same flag SignalEngineService uses for its own ATR check, because
        // both express one idea: "do not trade a quiet market". Turning the concept off should
        // turn off both.
        //
        // The flag, not the number, is the off-switch. Deactivating MinAtrPercent would restore
        // the compiled 0.10 default - ten times stricter - and a threshold has no safe "off"
        // value in general: 0 disables a MINIMUM but would block everything on a MAXIMUM.
        // Either switch turns this off: the Enable flag, or deactivating the threshold row itself.
        if (!ctx.StrategySettings.GetBool("EnableAtrFilter", ctx.Symbol, true)
            || !ctx.StrategySettings.IsSettingActive("MinAtrPercent", ctx.Symbol))
            return Task.FromResult<string?>(null);

        var minAtr = ctx.StrategySettings.GetDecimal("MinAtrPercent", ctx.Symbol, 0.10m);

        return Task.FromResult(market.AtrPct < minAtr
            ? $"ATR {market.AtrPct:F4}% < minimum {minAtr:F4}% ({ctx.Symbol})"
            : null);
    }
}
