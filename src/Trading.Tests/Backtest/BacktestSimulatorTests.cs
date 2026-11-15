using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Settings;
using Trading.Backtest.Simulation;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>Chronological execution: one position across symbols, simultaneous signals, cooldowns, stop-loss handling.</summary>
public class BacktestSimulatorTests
{
    private const double Ltp = 23940;       // CallPoint's last price with the default geometry

    private static (ExecutionSimulator Sim, BacktestSettings Settings) Create(Action<BacktestSettings>? tweak = null)
    {
        var s = BtSupport.LiveLike();
        tweak?.Invoke(s);
        var entry = new EntryEvaluator(s, BtSupport.Trading(), BtSupport.Instruments());
        return (new ExecutionSimulator(entry, s, BtSupport.Exit(s), options: null), s);
    }

    private static SimulationOptions Calls(Func<MarketPoint, bool>? reject = null) =>
        new(TradeSide.Call, StopModel.Live, PremiumMode.ModelOnly, Costs: null, RejectEntry: reject);

    private static int Index(DateTime utc) => (int)(utc - BtSupport.SessionOpenUtc).TotalMinutes;

    /// <summary>Flat until the entry minute, then a 50-point drop: with R = 15 premium points the stop is hit on the first bar after entry.</summary>
    private static Bar[] CrashAt(DateTime entryUtc) => BtSupport.Bars(i => i < Index(entryUtc) ? Ltp : Ltp - 50);

    private static void AssertInvariant(SimulationResult r) => Assert.Equal(r.TotalSignals, r.BlockedCount + r.Trades.Count);

    [Fact]
    public void StopLossIsHandledOnTheFirstBarAndBookedAtMinusR()
    {
        var (sim, _) = Create();
        var t0 = BtSupport.At(4, 30);
        var r = sim.Run([BtSupport.Data("NIFTY", CrashAt(t0), BtSupport.CallPoint("NIFTY", t0))], Calls());

        var trade = Assert.Single(r.Trades);
        Assert.Equal("OPTION_SL_HIT", trade.ExitReason);
        Assert.Equal(t0.AddMinutes(1), trade.ExitUtc);                  // exits at the close of the first bar after entry
        Assert.Equal(-15 * 65, trade.PnlRs);                            // R = clamp(20 x 1.5, 20, 100) x delta 0.5 = 15 points x lot size 65
        AssertInvariant(r);
    }

    [Fact]
    public void SimultaneousSignalsOnTwoSymbolsOpenExactlyOnePosition()
    {
        var (sim, _) = Create();
        var t0 = BtSupport.At(4, 30);
        var data = new[]
        {
            BtSupport.Data("NIFTY", BtSupport.FlatBars(Ltp), BtSupport.CallPoint("NIFTY", t0)),
            BtSupport.Data("SENSEX", BtSupport.FlatBars(Ltp), BtSupport.CallPoint("SENSEX", t0)),
        };
        var r = sim.Run(data, Calls());

        var trade = Assert.Single(r.Trades);
        Assert.Equal("NIFTY", trade.Symbol);                              // first in the supplied order wins the tie
        Assert.Equal(1, r.BlockedBy["OnePositionGate"]);
        AssertInvariant(r);
    }

    [Fact]
    public void ASignalWhileAPositionIsOpenIsBlockedUntilThePositionCloses()
    {
        var (sim, _) = Create();
        var t0 = BtSupport.At(4, 30);
        var data = new[]
        {
            BtSupport.Data("NIFTY", BtSupport.FlatBars(Ltp), BtSupport.CallPoint("NIFTY", t0), BtSupport.CallPoint("NIFTY", BtSupport.At(5, 0))),
            BtSupport.Data("SENSEX", BtSupport.FlatBars(Ltp), BtSupport.CallPoint("SENSEX", BtSupport.At(5, 30))),
        };
        var r = sim.Run(data, Calls());

        Assert.Single(r.Trades);                                          // flat bars never hit a stop or target: held to 15:15 IST
        Assert.Equal(2, r.BlockedBy["OnePositionGate"]);
        Assert.Equal("FORCE_EXIT_EOD", r.Trades[0].ExitReason);
        AssertInvariant(r);
    }

    [Fact]
    public void DuplicateCooldownBlocksTheSameSymbolAndDirectionForFiveMinutesAfterAClose()
    {
        var (sim, _) = Create();
        var t0 = BtSupport.At(4, 30);                                     // exits at 04:31
        var r = sim.Run([BtSupport.Data("NIFTY", CrashAt(t0), BtSupport.CallPoint("NIFTY", t0),
            BtSupport.CallPoint("NIFTY", BtSupport.At(4, 35)),            // 4 minutes after the exit: blocked
            BtSupport.CallPoint("NIFTY", BtSupport.At(4, 36)))], Calls()); // exactly 5 minutes after: allowed

        Assert.Equal(2, r.Trades.Count);
        Assert.Equal(t0, r.Trades[0].EntryUtc);
        Assert.Equal(BtSupport.At(4, 36), r.Trades[1].EntryUtc);
        Assert.Equal(1, r.BlockedBy["DuplicateSignalGate"]);
        AssertInvariant(r);
    }

    [Fact]
    public void HandoffCooldownBlocksEverySymbolForTenMinutesAfterAnyExit()
    {
        var (sim, _) = Create(s => s.Set("EnableHandoffCooldownGate", null, 1).Set("HandoffCooldownMinutes", null, 10));
        var t0 = BtSupport.At(4, 30);                                     // NIFTY exits at 04:31
        var r = sim.Run(
        [
            BtSupport.Data("NIFTY", CrashAt(t0), BtSupport.CallPoint("NIFTY", t0)),
            BtSupport.Data("SENSEX", BtSupport.FlatBars(Ltp),
                BtSupport.CallPoint("SENSEX", BtSupport.At(4, 40)),       // 9 minutes after the exit: blocked
                BtSupport.CallPoint("SENSEX", BtSupport.At(4, 41))),      // exactly 10 minutes: allowed
        ], Calls());

        Assert.Equal(["NIFTY", "SENSEX"], r.Trades.Select(t => t.Symbol).ToArray());
        Assert.Equal(BtSupport.At(4, 41), r.Trades[1].EntryUtc);
        Assert.Equal(1, r.BlockedBy["HandoffCooldownGate"]);
        AssertInvariant(r);
    }

    [Fact]
    public void RejectedEntryStartsAFiveMinuteCooldownWithoutOpeningAPosition()
    {
        var (sim, _) = Create();
        var rejectFirst = true;
        var r = sim.Run([BtSupport.Data("NIFTY", BtSupport.FlatBars(Ltp),
            BtSupport.CallPoint("NIFTY", BtSupport.At(4, 30)),
            BtSupport.CallPoint("NIFTY", BtSupport.At(4, 34)),            // 4 minutes after the rejection: blocked
            BtSupport.CallPoint("NIFTY", BtSupport.At(4, 35)))],          // 5 minutes: allowed
            Calls(reject: _ => { var rejected = rejectFirst; rejectFirst = false; return rejected; }));

        var trade = Assert.Single(r.Trades);
        Assert.Equal(BtSupport.At(4, 35), trade.EntryUtc);
        Assert.Equal(1, r.BlockedBy["OrderRejected"]);
        Assert.Equal(1, r.BlockedBy["DuplicateSignalGate"]);
        AssertInvariant(r);
    }

    [Fact]
    public void ASymbolsCooldownDoesNotBlockAnotherSymbolWhenHandoffIsOff()
    {
        var (sim, _) = Create();
        var t0 = BtSupport.At(4, 30);
        var r = sim.Run(
        [
            BtSupport.Data("NIFTY", CrashAt(t0), BtSupport.CallPoint("NIFTY", t0)),
            BtSupport.Data("SENSEX", BtSupport.FlatBars(Ltp), BtSupport.CallPoint("SENSEX", BtSupport.At(4, 33))),
        ], Calls());

        Assert.Equal(2, r.Trades.Count);
        Assert.False(r.BlockedBy.ContainsKey("DuplicateSignalGate"));
    }

    [Fact]
    public void GateBlocksCountAsSignalsAndNonSignalsDoNot()
    {
        var (sim, _) = Create();
        var t0 = BtSupport.At(4, 30);
        var quiet = BtSupport.CallPoint("NIFTY", BtSupport.At(4, 31), atrPct: 0.01);         // chain passes, AtrGate blocks
        var offBand = BtSupport.CallPoint("NIFTY", BtSupport.At(4, 32), rsi5: 80);           // chain fails: no signal at all
        var r = sim.Run([BtSupport.Data("NIFTY", BtSupport.FlatBars(Ltp), BtSupport.CallPoint("NIFTY", t0), quiet, offBand)], Calls());

        Assert.Equal(2, r.TotalSignals);
        Assert.Single(r.Trades);
        Assert.Equal(1, r.BlockedBy["AtrGate"]);
        AssertInvariant(r);
    }

    [Fact]
    public void ReplayingTheSameInputsTwiceGivesTheSameTrades()
    {
        var (sim, _) = Create();
        var t0 = BtSupport.At(4, 30);
        var data = new[] { BtSupport.Data("NIFTY", CrashAt(t0), BtSupport.CallPoint("NIFTY", t0), BtSupport.CallPoint("NIFTY", BtSupport.At(4, 40))) };

        var a = sim.Run(data, Calls());
        var b = sim.Run(data, Calls());
        Assert.Equal(a.Trades.Select(t => (t.EntryUtc, t.ExitUtc, t.PnlRs)), b.Trades.Select(t => (t.EntryUtc, t.ExitUtc, t.PnlRs)));
    }

    [Fact]
    public void ThePathNeverReadsBarsBeforeTheEntryMinute()
    {
        // a huge move BEFORE the entry must not affect the trade; the same move AFTER the entry must
        var (sim, _) = Create();
        var t0 = BtSupport.At(4, 30);
        var before = BtSupport.Bars(i => i < Index(t0) ? Ltp - 500 : Ltp);
        var after = BtSupport.Bars(i => i < Index(t0) ? Ltp : Ltp - 500);

        var a = sim.Run([BtSupport.Data("NIFTY", before, BtSupport.CallPoint("NIFTY", t0))], Calls()).Trades.Single();
        var b = sim.Run([BtSupport.Data("NIFTY", after, BtSupport.CallPoint("NIFTY", t0))], Calls()).Trades.Single();

        Assert.Equal(0, a.PnlRs);
        Assert.Equal("OPTION_SL_HIT", b.ExitReason);
    }
}
