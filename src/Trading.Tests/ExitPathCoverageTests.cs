using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Worker;
using Trading.Worker.Exits;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Regression cover for the two defects that left signal 20 (NIFTY HERO_ZERO, 25 Aug 2026)
/// completely unmanaged by the exit path:
///
///   A. ExitWorker's missing-leg2 guard tested `StrategyType != SINGLE`, which also matched
///      HERO_ZERO (single-leg, so leg2 is always 0) and skipped it every cycle.
///   B. ExitDataLoader loaded ONE option-chain snapshot globally, but each SnapshotId covers a
///      single symbol — so only the last-written symbol (SENSEX) was visible and NIFTY/BANKNIFTY
///      positions resolved optionLtp = 0.
/// </summary>
public class ExitPathCoverageTests
{
    // ── A. missing-leg2 guard ────────────────────────────────────────────────

    [Theory]
    [InlineData(TradingConst.StrategyType.Single)]
    [InlineData(TradingConst.StrategyType.HeroZero)]
    public void SingleLegStrategies_AreNeverSkipped_WhenLeg2IsZero(string strategyType)
    {
        // Both are single-leg: leg2 is legitimately 0 and must not disqualify them.
        Assert.False(ExitWorker.ShouldSkipMissingLeg2(strategyType, leg2Ltp: 0m, forceExit: false));
    }

    [Theory]
    [InlineData(TradingConst.StrategyType.BullSpread)]
    [InlineData(TradingConst.StrategyType.BearSpread)]
    [InlineData(TradingConst.StrategyType.Spread)]
    public void Spreads_AreSkipped_WhenLeg2HasNoPrice(string strategyType)
    {
        Assert.True(ExitWorker.ShouldSkipMissingLeg2(strategyType, leg2Ltp: 0m, forceExit: false));
    }

    [Theory]
    [InlineData(TradingConst.StrategyType.BullSpread)]
    [InlineData(TradingConst.StrategyType.BearSpread)]
    public void Spreads_AreNotSkipped_OnceLeg2HasAPrice(string strategyType)
    {
        Assert.False(ExitWorker.ShouldSkipMissingLeg2(strategyType, leg2Ltp: 12.5m, forceExit: false));
    }

    [Theory]
    [InlineData(TradingConst.StrategyType.Single)]
    [InlineData(TradingConst.StrategyType.HeroZero)]
    [InlineData(TradingConst.StrategyType.BullSpread)]
    [InlineData(TradingConst.StrategyType.BearSpread)]
    public void ForceExit_OverridesTheGuard_ForEveryStrategy(string strategyType)
    {
        // EOD force exit must reach the ladder even with no leg2 price at all.
        Assert.False(ExitWorker.ShouldSkipMissingLeg2(strategyType, leg2Ltp: 0m, forceExit: true));
    }

    [Fact]
    public void HeroZero_WasTheRegression_GuardMustNotMatchIt()
    {
        // The exact production case: HERO_ZERO, no leg2, mid-session. The old
        // `StrategyType != SINGLE` form returned true here and skipped the whole exit ladder.
        var oldBuggyForm = TradingConst.StrategyType.HeroZero != TradingConst.StrategyType.Single
                           && 0m == 0m
                           && !false;
        Assert.True(oldBuggyForm, "sanity: the old form did match HERO_ZERO");

        Assert.False(ExitWorker.ShouldSkipMissingLeg2(
            TradingConst.StrategyType.HeroZero, leg2Ltp: 0m, forceExit: false));
    }

    // ── B. per-symbol chain snapshot ─────────────────────────────────────────

    private static OptionChainData Row(string symbol, string snapshotId, DateTime createdOn, string tradingSymbol) =>
        new()
        {
            Symbol = symbol,
            SnapshotId = snapshotId,
            CreatedOn = createdOn,
            TradingSymbol = tradingSymbol,
            Ltp = 100m,
        };

    [Fact]
    public void EverySymbol_SurvivesSelection_EvenWhenAnotherSymbolIsNewer()
    {
        // Production ordering: NIFTY written first, BANKNIFTY second, SENSEX last. The old code
        // kept only SENSEX.
        var t = new DateTime(2026, 8, 25, 14, 30, 0);
        var recent = new List<OptionChainData>
        {
            Row("NIFTY",     "snap-nifty",  t.AddSeconds(0),  "NIFTY26AUG24200CE"),
            Row("BANKNIFTY", "snap-bank",   t.AddSeconds(5),  "BANKNIFTY26AUG57400PE"),
            Row("SENSEX",    "snap-sensex", t.AddSeconds(13), "SENSEX26AUG80000CE"),
        };

        // What the old code did: take the single newest SnapshotId across all symbols.
        var oldSingleSnapshotId = recent.OrderByDescending(x => x.CreatedOn).First().SnapshotId;
        var oldResult = recent.Where(x => x.SnapshotId == oldSingleSnapshotId)
                              .GroupBy(x => x.Symbol)
                              .ToDictionary(g => g.Key, g => g.ToList());
        Assert.Single(oldResult);                        // only SENSEX survived
        Assert.False(oldResult.ContainsKey("NIFTY"));    // ← the bug: NIFTY invisible to exits

        var result = ExitDataLoader.SelectLatestPerSymbol(recent);

        Assert.Equal(3, result.Count);
        Assert.True(result.ContainsKey("NIFTY"));
        Assert.True(result.ContainsKey("BANKNIFTY"));
        Assert.True(result.ContainsKey("SENSEX"));
    }

    [Fact]
    public void OnlyTheNewestSnapshotPerSymbol_IsKept()
    {
        var t = new DateTime(2026, 8, 25, 14, 30, 0);
        var recent = new List<OptionChainData>
        {
            Row("NIFTY", "snap-old", t.AddSeconds(0),  "NIFTY26AUG24200CE"),
            Row("NIFTY", "snap-old", t.AddSeconds(1),  "NIFTY26AUG24300CE"),
            Row("NIFTY", "snap-new", t.AddSeconds(50), "NIFTY26AUG24200CE"),
            Row("NIFTY", "snap-new", t.AddSeconds(51), "NIFTY26AUG24300CE"),
        };

        var result = ExitDataLoader.SelectLatestPerSymbol(recent);

        Assert.Equal(2, result["NIFTY"].Count);
        Assert.All(result["NIFTY"], r => Assert.Equal("snap-new", r.SnapshotId));
    }

    [Fact]
    public void StaleAndFreshSnapshots_AreNotMixedWithinASymbol()
    {
        // A half-written newer snapshot must not be padded out with rows from the previous one.
        var t = new DateTime(2026, 8, 25, 14, 30, 0);
        var recent = new List<OptionChainData>
        {
            Row("BANKNIFTY", "snap-a", t.AddSeconds(0),  "BANKNIFTY26AUG57400PE"),
            Row("BANKNIFTY", "snap-a", t.AddSeconds(1),  "BANKNIFTY26AUG57500PE"),
            Row("BANKNIFTY", "snap-b", t.AddSeconds(45), "BANKNIFTY26AUG57400PE"),
        };

        var result = ExitDataLoader.SelectLatestPerSymbol(recent);

        var single = Assert.Single(result["BANKNIFTY"]);
        Assert.Equal("snap-b", single.SnapshotId);
    }

    [Fact]
    public void EmptyInput_YieldsEmptyResult_NotAnException()
    {
        Assert.Empty(ExitDataLoader.SelectLatestPerSymbol(new List<OptionChainData>()));
    }

    [Fact]
    public void ResolveLtps_FindsTheOption_ForANonNewestSymbol()
    {
        // End-to-end shape of the bug: a NIFTY signal must resolve a real premium even though
        // SENSEX wrote its snapshot more recently.
        var t = new DateTime(2026, 8, 25, 14, 30, 0);
        var recent = new List<OptionChainData>
        {
            new()
            {
                Symbol = "NIFTY", SnapshotId = "snap-nifty", CreatedOn = t,
                TradingSymbol = "NIFTY26AUG24200CE", Ltp = 41.90m, Delta = 0.45m,
            },
            Row("SENSEX", "snap-sensex", t.AddSeconds(13), "SENSEX26AUG80000CE"),
        };

        var chainBySymbol = ExitDataLoader.SelectLatestPerSymbol(recent);

        var signal = new Signal
        {
            Symbol = "NIFTY",
            OptionSymbol = "NIFTY26AUG24200CE",
            StrategyType = TradingConst.StrategyType.HeroZero,
        };

        var (optionLtp, _, delta) = ExitDataLoader.ResolveLtps(signal, chainBySymbol);

        Assert.Equal(41.90m, optionLtp);   // was 0 before the fix — which killed every option exit
        Assert.Equal(0.45m, delta);
    }
}
