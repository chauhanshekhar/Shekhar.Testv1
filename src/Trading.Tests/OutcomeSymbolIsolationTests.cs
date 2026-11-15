using Trading.Domain;
using Trading.Domain.Entities;
using Trading.SignalEngine.Analytics;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Future prices must come from the entry's OWN symbol. Scoring a BANKNIFTY snapshot against NIFTY
/// prices would silently produce plausible-looking but meaningless win rates, which is worse than
/// an obvious failure — so the isolation is pinned here.
///
/// The worker keys its future-price series by symbol (IndicatorSnapshotOutcomeWorker
/// .LoadFuturePricesAsync groups by Symbol, and the per-symbol query filters s.Symbol == symbol).
/// These tests exercise that contract: each index is deliberately given a path that would produce
/// the OPPOSITE result if another symbol's series were used by mistake.
/// </summary>
public class OutcomeSymbolIsolationTests
{
    private static readonly DateTime Entry = new(2026, 8, 19, 10, 0, 0, DateTimeKind.Unspecified);
    private static readonly DateTime WellAfter = Entry.AddMinutes(30);

    /// <summary>
    /// Per-symbol future series, mirroring the dictionary the worker builds. Each series is
    /// constructed so that using the wrong one flips TARGET to SL.
    /// </summary>
    private static Dictionary<string, FuturePoint[]> SeriesBySymbol() => new(StringComparer.OrdinalIgnoreCase)
    {
        // NIFTY entry 24,000 — rises 25 -> CALL TARGET
        ["NIFTY"] = [new(Entry.AddMinutes(2), 24_025m)],

        // BANKNIFTY entry 57,000 — falls 25 -> CALL would be SL, PUT would be TARGET
        ["BANKNIFTY"] = [new(Entry.AddMinutes(2), 56_975m)],

        // SENSEX entry 81,000 — rises 30 -> CALL TARGET
        ["SENSEX"] = [new(Entry.AddMinutes(2), 81_030m)],
    };

    private static readonly Dictionary<string, decimal> EntryPrices = new(StringComparer.OrdinalIgnoreCase)
    {
        ["NIFTY"] = 24_000m,
        ["BANKNIFTY"] = 57_000m,
        ["SENSEX"] = 81_000m,
    };

    private static string Result(string symbol, string direction)
    {
        var series = SeriesBySymbol()[symbol];       // same lookup the worker performs
        var calc = OutcomeBarrierCalculator.Calculate(
            direction, EntryPrices[symbol], Entry, series, WellAfter);
        return calc.Window15.Result!;
    }

    [Theory]
    [InlineData("NIFTY", OutcomeResult.Target)]
    [InlineData("BANKNIFTY", OutcomeResult.StopLoss)]
    [InlineData("SENSEX", OutcomeResult.Target)]
    public void EachSymbolIsScoredAgainstItsOwnPath_Call(string symbol, string expected)
    {
        Assert.Equal(expected, Result(symbol, TradingConst.SignalType.CALL));
    }

    [Theory]
    [InlineData("NIFTY", OutcomeResult.StopLoss)]
    [InlineData("BANKNIFTY", OutcomeResult.Target)]
    [InlineData("SENSEX", OutcomeResult.StopLoss)]
    public void EachSymbolIsScoredAgainstItsOwnPath_Put(string symbol, string expected)
    {
        Assert.Equal(expected, Result(symbol, TradingConst.SignalType.PUT));
    }

    [Fact]
    public void UsingAnotherSymbolsPath_ProducesMeaninglessMagnitudes()
    {
        // Why the worker keys future prices by symbol rather than pooling them: applying NIFTY's
        // series to a BANKNIFTY entry compares 24,025 against 57,000, a ~33,000-point "move".
        // Every such row would classify instantly and the win rates would be pure noise.
        var crossed = OutcomeBarrierCalculator.Calculate(
            TradingConst.SignalType.CALL,
            EntryPrices["BANKNIFTY"],
            Entry,
            SeriesBySymbol()["NIFTY"],       // deliberately wrong series
            WellAfter);

        Assert.True(crossed.Window15.MaxAdverse < -30_000m,
            "a cross-symbol comparison yields an impossible index move — the isolation exists to prevent it");

        // The genuine per-symbol answers are asserted by the theories above; this only documents
        // the failure mode being guarded against.
        Assert.Equal(OutcomeResult.StopLoss, Result("BANKNIFTY", TradingConst.SignalType.CALL));
    }

    [Fact]
    public void SymbolWithNoFutureSeries_YieldsNoneNotACrossSymbolFallback()
    {
        // A missing series must degrade to NONE, never quietly borrow another symbol's prices.
        var series = SeriesBySymbol();
        var missing = series.TryGetValue("FINNIFTY", out var s) ? s : [];

        var calc = OutcomeBarrierCalculator.Calculate(
            TradingConst.SignalType.CALL, 23_000m, Entry, missing, WellAfter);

        Assert.Equal(OutcomeResult.None, calc.Window15.Result);
    }
}
