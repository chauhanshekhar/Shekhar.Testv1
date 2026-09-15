using Trading.Worker.SignalGates;

namespace Trading.Worker.Strategies;

/// <summary>
/// Resolves symbol -> <see cref="IInstrumentStrategy"/>. Every registered override is asked first;
/// <see cref="DefaultInstrumentStrategy"/> is the fallback and is deliberately NOT part of that
/// collection, so adding an override can never be defeated by DI registration order.
///
/// To give one instrument its own logic: implement <see cref="IInstrumentStrategy"/>, return true
/// from Handles for that symbol only, and register it as IInstrumentStrategy in
/// SignalEngineExtensions. Every other symbol is unaffected. If two overrides claim the same
/// symbol the resolution would be registration-order dependent, so that is rejected at startup
/// rather than left to chance — see <see cref="ValidateRegistrations"/>.
/// </summary>
public class InstrumentStrategyRegistry
{
    private readonly IReadOnlyList<IInstrumentStrategy> _overrides;
    private readonly DefaultInstrumentStrategy _fallback;

    public InstrumentStrategyRegistry(
        IEnumerable<IInstrumentStrategy> overrides,
        DefaultInstrumentStrategy fallback)
    {
        _overrides = overrides.ToList();
        _fallback = fallback;
    }

    public IInstrumentStrategy For(string symbol)
    {
        foreach (var strategy in _overrides)
        {
            if (strategy.Handles(symbol))
                return strategy;
        }

        return _fallback;
    }

    /// <summary>
    /// Fails fast at startup if more than one override claims the same symbol. Silently picking
    /// the first would mean a live instrument trades under a strategy nobody chose.
    /// </summary>
    public void ValidateRegistrations(IEnumerable<string> activeSymbols)
    {
        foreach (var symbol in activeSymbols)
        {
            var claimants = _overrides.Where(s => s.Handles(symbol)).ToList();
            if (claimants.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Symbol '{symbol}' is claimed by {claimants.Count} instrument strategies " +
                    $"({string.Join(", ", claimants.Select(c => c.GetType().Name))}). " +
                    "Exactly one strategy may own a symbol.");
            }
        }
    }
}
