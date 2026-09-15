using Trading.Worker.SignalGates;

namespace Trading.Worker.Strategies;

/// <summary>
/// The shared strategy every instrument uses unless one is explicitly overridden — a thin
/// delegation to <see cref="StrategyManager"/>, which keeps the existing
/// StrategySelectorService -> Directional/HeroZero/Expiry dispatch as the single implementation
/// of the trading logic. No decision is duplicated here; this type exists purely to give the
/// registry a fallback to resolve to.
///
/// <see cref="Handles"/> is always false: the registry holds this instance separately and uses it
/// only after every registered <see cref="IInstrumentStrategy"/> has declined the symbol, so it can
/// never shadow a real per-instrument override through registration order.
/// </summary>
public class DefaultInstrumentStrategy : IInstrumentStrategy
{
    private readonly StrategyManager _strategyManager;

    public DefaultInstrumentStrategy(StrategyManager strategyManager)
    {
        _strategyManager = strategyManager;
    }

    public bool Handles(string symbol) => false;

    public Task<SignalSelectionResult> GenerateAsync(
        SignalContext ctx,
        MarketSnapshot market,
        string symbol,
        CancellationToken ct)
        => _strategyManager.SelectSignalAsync(ctx, market, symbol);
}
