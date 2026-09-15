using Trading.Worker;
using Trading.Worker.SignalGates;
using Trading.Worker.Strategies;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Resolution rules for the per-instrument signal-generation seam. These matter because the
/// registry decides which code trades a live symbol: silently falling through to the wrong
/// strategy, or letting registration order pick between two claimants, would route real orders
/// through logic nobody selected.
///
/// DefaultInstrumentStrategy is not constructed here — it needs the full StrategyManager graph,
/// and what is under test is the resolution rule, not the delegation (which is a one-line call
/// into StrategyManager.SelectSignalAsync).
/// </summary>
public class InstrumentStrategyRegistryTests
{
    /// <summary>Override that claims exactly the symbols it is told to.</summary>
    private sealed class StubStrategy : IInstrumentStrategy
    {
        private readonly string[] _symbols;
        public StubStrategy(params string[] symbols) => _symbols = symbols;

        public bool Handles(string symbol) => _symbols.Contains(symbol);

        public Task<SignalSelectionResult> GenerateAsync(
            SignalContext ctx, MarketSnapshot market, string symbol, CancellationToken ct) =>
            throw new NotSupportedException("resolution-only stub");
    }

    // The registry takes DefaultInstrumentStrategy as a concrete type, so a null-forgiving
    // sentinel stands in for it: these tests only assert WHICH instance comes back.
    private static InstrumentStrategyRegistry Build(params IInstrumentStrategy[] overrides) =>
        new(overrides, fallback: null!);

    [Fact]
    public void SymbolWithNoOverride_ResolvesToFallback()
    {
        var registry = Build(new StubStrategy("SENSEX"));

        // null is the fallback sentinel — NIFTY must not be captured by the SENSEX override.
        Assert.Null(registry.For("NIFTY"));
    }

    [Fact]
    public void EverySymbol_ResolvesToFallback_WhenNoOverridesRegistered()
    {
        var registry = Build();

        foreach (var symbol in new[] { "NIFTY", "BANKNIFTY", "SENSEX", "FINNIFTY", "MIDCAPNIFTY" })
            Assert.Null(registry.For(symbol));
    }

    [Fact]
    public void SymbolWithOverride_ResolvesToThatOverride()
    {
        var sensex = new StubStrategy("SENSEX");
        var registry = Build(sensex);

        Assert.Same(sensex, registry.For("SENSEX"));
    }

    [Fact]
    public void OverrideIsNotShadowedByRegistrationOrder()
    {
        // The fallback is held separately, never inside the override collection, so it cannot
        // win by being registered first.
        var sensex = new StubStrategy("SENSEX");
        var registry = Build(new StubStrategy("BANKNIFTY"), sensex);

        Assert.Same(sensex, registry.For("SENSEX"));
    }

    [Fact]
    public void TwoOverridesClaimingOneSymbol_FailFastAtStartup()
    {
        var registry = Build(new StubStrategy("SENSEX"), new StubStrategy("SENSEX", "NIFTY"));

        var ex = Assert.Throws<InvalidOperationException>(
            () => registry.ValidateRegistrations(new[] { "NIFTY", "SENSEX" }));

        Assert.Contains("SENSEX", ex.Message);
    }

    [Fact]
    public void DistinctOverrides_PassValidation()
    {
        var registry = Build(new StubStrategy("SENSEX"), new StubStrategy("NIFTY"));

        registry.ValidateRegistrations(new[] { "NIFTY", "BANKNIFTY", "SENSEX" });
    }
}
