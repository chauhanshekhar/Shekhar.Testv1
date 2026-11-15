using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>HandoffCooldownGate: no same-direction entry on any symbol within N minutes of an exit (paper: 1 win / 9 losses, -10,006).</summary>
public class HandoffCooldownGateTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 11, 0, 0);

    [Theory]
    [InlineData(1)]
    [InlineData(120)]
    [InlineData(299)]
    public void ExitInsideWindow_Blocks(int secondsAgo)
    {
        var reason = HandoffCooldownGate.Evaluate("CALL", "SENSEX", Now.AddSeconds(-secondsAgo), "OPTION_SL_HIT", Now, 5);

        Assert.NotNull(reason);
        Assert.Contains("SENSEX", reason);
    }

    [Theory]
    [InlineData(300)]
    [InlineData(900)]
    public void ExitOutsideWindow_Proceeds(int secondsAgo)
        => Assert.Null(HandoffCooldownGate.Evaluate("CALL", "SENSEX", Now.AddSeconds(-secondsAgo), "OPTION_SL_HIT", Now, 5));

    [Fact]
    public void NoRecentExit_Proceeds()
        => Assert.Null(HandoffCooldownGate.Evaluate("CALL", null, null, null, Now, 5));

    [Fact]
    public void ZeroMinutes_Disables()
        => Assert.Null(HandoffCooldownGate.Evaluate("CALL", "SENSEX", Now.AddSeconds(-1), "X", Now, 0));

    [Fact]
    public void ExitInFuture_Proceeds()
        => Assert.Null(HandoffCooldownGate.Evaluate("CALL", "SENSEX", Now.AddSeconds(30), "X", Now, 5));
}
