using Xunit;
using Action = Trading.Worker.Exits.ExitDecisionService.T2PartialAction;
using Svc = Trading.Worker.Exits.ExitDecisionService;

namespace Trading.Tests;

/// <summary>
/// EnableT2Partial: with 3+ lots T1 sells half, T2 sells half of the rest, the last lot is held for
/// the stops / trail. 1-2 lot trades keep the legacy behaviour (None = fall through to steps 7/8).
/// Entry 100, T1 120, T2 126 in these cases.
/// </summary>
public class T2PartialTests
{
    [Theory]
    [InlineData(119, 4, 4, Action.None)]       // below T1
    [InlineData(121, 4, 4, Action.None)]       // T1 band: step 8 books the T1 partial
    [InlineData(126, 4, 4, Action.SellAtT1)]   // gapped past T2 before any partial: T1 sale first
    [InlineData(126, 2, 4, Action.SellAtT2)]   // T1 half done (4 -> 2): T2 sells 1
    [InlineData(125, 2, 4, Action.None)]       // between T1 and T2
    [InlineData(130, 1, 4, Action.Hold)]       // last lot: do not full-exit at T1/T2, trail owns it
    [InlineData(121, 1, 4, Action.Hold)]
    [InlineData(119, 1, 4, Action.None)]       // below T1: nothing to suppress
    [InlineData(126, 1, 3, Action.Hold)]       // 3 lots: T1 sells 2, runner of 1 trails
    [InlineData(126, 3, 3, Action.SellAtT1)]
    public void Routes_ThreePlusLots(double ltp, int open, int original, Action expected)
        => Assert.Equal(expected, Svc.DecideT2Partial((decimal)ltp, 120m, 126m, open, original));

    [Theory]
    [InlineData(126, 1, 1)]
    [InlineData(126, 2, 2)]
    [InlineData(126, 1, 2)]
    public void OneAndTwoLots_KeepLegacyBehaviour(double ltp, int open, int original)
        => Assert.Equal(Action.None, Svc.DecideT2Partial((decimal)ltp, 120m, 126m, open, original));

    [Fact]
    public void NoT2_IsNone()
        => Assert.Equal(Action.None, Svc.DecideT2Partial(130m, 120m, 0m, 4, 4));
}
