using Trading.Domain.Entities;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// BreakoutProtection - while waiting for a pullback, a fresh close beyond the running day extreme (+/- 5 pt buffer)
/// moves the breakout level to that extreme instead of leaving it at the first, now-stale level. Without the reset a
/// trend that keeps extending never comes back within 5 pt of the original level, so the gate blocks all session.
/// </summary>
public class BreakoutProtectionResetTests
{
    private static readonly DateTime Start = new(2026, 11, 20, 3, 45, 0, DateTimeKind.Utc);

    private static List<Candle> Candles(params (decimal High, decimal Low, decimal Close)[] bars) =>
        bars.Select((b, i) => new Candle
        {
            Symbol = "NIFTY",
            CandleTime = Start.AddMinutes(i),
            Open = b.Close,
            High = b.High,
            Low = b.Low,
            Close = b.Close
        }).ToList();

    private static void AssertOnlyLastAllowed(BreakoutDecision[] decisions)
    {
        for (var i = 0; i < decisions.Length - 1; i++)
            Assert.False(decisions[i].Allowed, $"candle {i} should be blocked: {decisions[i].Reason}");

        Assert.True(decisions[^1].Allowed, $"last candle should be allowed: {decisions[^1].Reason}");
    }

    [Fact]
    public void Put_SecondBreakdownBeforePullback_MovesLevel_ThenPullbackAndContinuationAllow()
    {
        var decisions = BreakoutProtectionService.ReplaySession(
            Candles((100, 95, 97), (96, 88, 89), (89, 80, 81), (84, 82, 83), (83, 78, 79)),
            isCall: false);

        Assert.Contains("level moved", decisions[2].Reason);
        AssertOnlyLastAllowed(decisions);
    }

    [Fact]
    public void Call_SecondBreakoutBeforePullback_MovesLevel_ThenPullbackAndContinuationAllow()
    {
        var decisions = BreakoutProtectionService.ReplaySession(
            Candles((105, 100, 103), (112, 104, 111), (120, 111, 119), (118, 116, 117), (122, 117, 121)),
            isCall: true);

        Assert.Contains("level moved", decisions[2].Reason);
        AssertOnlyLastAllowed(decisions);
    }

    [Fact]
    public void Put_PlainBreakdownPullbackContinuation_StillAllows()
    {
        var decisions = BreakoutProtectionService.ReplaySession(
            Candles((100, 95, 97), (96, 88, 89), (91, 88, 90), (89, 85, 86)),
            isCall: false);

        AssertOnlyLastAllowed(decisions);
    }
}
