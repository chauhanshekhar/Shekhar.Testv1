using Trading.Domain.Entities;
using Trading.Worker.Exits;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Option price resolution: the live quote wins, the chain snapshot is the fallback.
///
/// Why this matters more than telemetry: option prices previously reached ExitWorker only through
/// OptionChainData, written every 30 seconds and accepted up to 2 minutes stale. The worker runs
/// every second, so MfePrice/MaePrice AND the OptionSL/T1/T2 exit checks were all evaluated on a
/// price that moved twice a minute. On 2026-08-31 signal 41 recorded MAE == MFE == 560.90 — a
/// single observation across a 55-second trade.
///
/// The fallback path must keep working: a quote failure leaves the symbol out of the dictionary,
/// and resolution has to degrade to the chain rather than to zero.
/// </summary>
public class LiveOptionQuoteTests
{
    private const string OptionSymbol = "SENSEX2690377000CE";

    private static Signal SingleLeg() => new()
    {
        Symbol = "SENSEX",
        StrategyType = "SINGLE",
        OptionSymbol = OptionSymbol,
    };

    /// <summary>Chain snapshot carrying a stale premium and the delta.</summary>
    private static Dictionary<string, List<OptionChainData>> Chain(decimal ltp, decimal delta = 0.6m) => new()
    {
        ["SENSEX"] =
        [
            new OptionChainData { TradingSymbol = OptionSymbol, Ltp = ltp, Delta = delta }
        ]
    };

    [Fact]
    public void LiveQuote_OverridesTheStaleChainPrice()
    {
        var live = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [OptionSymbol] = 520.05m          // the price that mattered
        };

        var (optionLtp, _, delta) = ExitDataLoader.ResolveLtps(SingleLeg(), Chain(483.25m), live);

        Assert.Equal(520.05m, optionLtp);
        Assert.Equal(0.6m, delta);            // greeks still come from the chain
    }

    [Fact]
    public void NoLiveQuote_FallsBackToTheChain()
    {
        var (optionLtp, _, _) = ExitDataLoader.ResolveLtps(
            SingleLeg(), Chain(483.25m), new Dictionary<string, decimal>());

        Assert.Equal(483.25m, optionLtp);
    }

    [Fact]
    public void NullLiveQuotes_BehavesExactlyAsBefore()
    {
        // The parameter is optional so every existing call site is unaffected.
        var (optionLtp, _, _) = ExitDataLoader.ResolveLtps(SingleLeg(), Chain(483.25m));

        Assert.Equal(483.25m, optionLtp);
    }

    [Fact]
    public void ZeroOrNegativeLiveQuote_IsIgnored()
    {
        // A bad quote must never replace a good chain price with nothing.
        foreach (var bad in new[] { 0m, -1m })
        {
            var live = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
            {
                [OptionSymbol] = bad
            };

            var (optionLtp, _, _) = ExitDataLoader.ResolveLtps(SingleLeg(), Chain(483.25m), live);

            Assert.Equal(483.25m, optionLtp);
        }
    }

    [Fact]
    public void QuoteForADifferentSymbol_IsNotApplied()
    {
        var live = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["SENSEX2690376900PE"] = 999m     // another leg entirely
        };

        var (optionLtp, _, _) = ExitDataLoader.ResolveLtps(SingleLeg(), Chain(483.25m), live);

        Assert.Equal(483.25m, optionLtp);
    }

    [Fact]
    public void Signal38_WouldHaveSeenThePriceThatPassedItsTarget()
    {
        // Entry 483.25, OptionT1 512.89, peak 520.05. With the live quote the exit ladder is
        // handed 520.05 and rule 8 (triggerLtp >= OptionT1) is satisfied.
        var live = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            [OptionSymbol] = 520.05m
        };

        var (optionLtp, _, _) = ExitDataLoader.ResolveLtps(SingleLeg(), Chain(483.25m), live);

        Assert.True(optionLtp >= 512.89m);
    }
}
