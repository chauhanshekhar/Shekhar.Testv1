using Trading.Backtest.Simulation;
using Trading.Domain.Entities;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>
/// --tick-check (research only): compares the replay's exit with the per-second option path ExitWorker recorded.
/// Only QUOTE ticks count; CHAIN ticks can be ~30 s stale.
/// </summary>
public class TickPathCheckTests
{
    private static readonly DateTime T0 = new(2026, 11, 25, 10, 57, 0);

    private static List<SignalOptionTick> Path(string source, decimal start, int count, decimal step, decimal optionSl = 100m, decimal protectedSl = 0m)
    {
        var rows = new List<SignalOptionTick>();
        for (var i = 0; i < count; i++)
            rows.Add(new SignalOptionTick
            {
                SignalId = 1, Symbol = "SENSEX", OptionSymbol = "X", TickTime = T0.AddSeconds(i),
                OptionLtp = start + step * i, Source = source, OptionSL = optionSl, ProtectedSL = protectedSl, Status = "Active",
            });
        return rows;
    }

    [Fact]
    public void QuoteTickTouchesStop_LiveStopped_ReplayDifferent_FlagsMinutePathMissedStop()
    {
        // 40 QUOTE ticks falling 1 point per second from 130; protected stop 100 is touched at tick 30.
        var ticks = Path(SignalOptionTickSource.Quote, 130m, 40, -1m, optionSl: 90m, protectedSl: 100m);

        var r = TickPathCheck.Evaluate(1, "SENSEX", "PROTECTED_SL_HIT", T0.AddSeconds(30), "TRAILING_SL_HIT", 4386.08, 0m, ticks);

        Assert.Equal(TickPathCheck.MinuteMissedStop, r.Verdict);
        Assert.Equal("ProtectedSL", r.TouchedStop);
        Assert.Equal(T0.AddSeconds(30), r.FirstStopTouch);
        Assert.Equal(40, r.QuoteTicks);
    }

    [Fact]
    public void NoStopTouched_IsSame()
    {
        var ticks = Path(SignalOptionTickSource.Quote, 130m, 40, 1m);

        var r = TickPathCheck.Evaluate(1, "SENSEX", "PROTECTED_SL_HIT", null, "TRAILING_SL_HIT", 100, 0m, ticks);

        Assert.Equal(TickPathCheck.Same, r.Verdict);
        Assert.Null(r.FirstStopTouch);
    }

    [Fact]
    public void StopTouched_ButReplayExitedForSameReason_IsSame()
    {
        var ticks = Path(SignalOptionTickSource.Quote, 130m, 40, -1m, protectedSl: 100m);

        var r = TickPathCheck.Evaluate(1, "SENSEX", "PROTECTED_SL_HIT", null, "PROTECTED_SL_HIT", 0, 0m, ticks);

        Assert.Equal(TickPathCheck.Same, r.Verdict);
    }

    [Fact]
    public void OnlyChainTicks_AreInsufficient_EvenWhenTheyCrossTheStop()
    {
        // Stale chain snapshots must never be used as evidence of a stop touch.
        var ticks = Path(SignalOptionTickSource.Chain, 130m, 60, -1m, protectedSl: 100m);

        var r = TickPathCheck.Evaluate(1, "SENSEX", "PROTECTED_SL_HIT", null, "TRAILING_SL_HIT", 0, 0m, ticks);

        Assert.Equal(TickPathCheck.TicksInsufficient, r.Verdict);
        Assert.Equal(0, r.QuoteTicks);
        Assert.Equal(60, r.ChainTicks);
        Assert.Null(r.FirstStopTouch);
    }

    [Fact]
    public void TooFewQuoteTicks_AreInsufficient()
    {
        var ticks = Path(SignalOptionTickSource.Quote, 130m, TickPathCheck.MinQuoteTicks - 1, -5m, protectedSl: 100m);

        Assert.Equal(TickPathCheck.TicksInsufficient,
            TickPathCheck.Evaluate(1, "SENSEX", "PROTECTED_SL_HIT", null, "TRAILING_SL_HIT", 0, 0m, ticks).Verdict);
    }

    [Fact]
    public void NoneSourceTicks_AreIgnored()
    {
        var ticks = Path(SignalOptionTickSource.None, 0m, 60, 0m, protectedSl: 100m);

        var r = TickPathCheck.Evaluate(1, "SENSEX", "PROTECTED_SL_HIT", null, "TRAILING_SL_HIT", 0, 0m, ticks);

        Assert.Equal(TickPathCheck.TicksInsufficient, r.Verdict);
    }

    [Fact]
    public void Csv_HasHeaderAndOneLinePerRow()
    {
        var ticks = Path(SignalOptionTickSource.Quote, 130m, 40, -1m, protectedSl: 100m);
        var row = TickPathCheck.Evaluate(1, "SENSEX", "PROTECTED_SL_HIT", T0, "TRAILING_SL_HIT", 1.5, -2m, ticks);

        var lines = TickPathCheck.ToCsv([row]).Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(2, lines.Length);
        Assert.StartsWith("SignalId,Symbol,", lines[0]);
        Assert.Contains("MINUTE_PATH_MISSED_STOP", lines[1]);
    }
}
