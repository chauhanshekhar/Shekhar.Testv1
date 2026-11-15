using Trading.Domain.Entities;
using Trading.Infrastructure.Brokers.Groww;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Scalping-state signals are paper-only. A position must always close the way it opened, so once an
/// entry order id exists it (PAPER- prefix) decides, not the market state.
/// </summary>
public class ScalpingPaperRoutingTests
{
    private static Signal Sig(string state, string brokerOrderId = "")
        => new() { MarketState = state, BrokerOrderId = brokerOrderId };

    [Theory]
    [InlineData(TradingMode.Live, "Scalping")]
    [InlineData(TradingMode.HeroZero, "Scalping")]
    [InlineData(TradingMode.Live, "Reversal")]
    [InlineData(TradingMode.HeroZero, "Reversal")]
    public void NewPaperOnlySignal_InRealMoneyMode_IsPaper(TradingMode mode, string state)
        => Assert.True(OrderRouter.RoutesToPaper(mode, Sig(state), scalpingPaperOnly: true));

    [Fact]
    public void ReversalSignalOpenedLive_ClosesLive()
        => Assert.False(OrderRouter.RoutesToPaper(TradingMode.Live, Sig("Reversal", "GRW123456"), scalpingPaperOnly: true));

    [Fact]
    public void KillSwitchOff_ReversalFollowsGlobalMode()
        => Assert.False(OrderRouter.RoutesToPaper(TradingMode.Live, Sig("Reversal"), scalpingPaperOnly: false));

    [Theory]
    [InlineData("Trend")]
    [InlineData("Range")]
    [InlineData("Breakout")]
    [InlineData("Breakdown")]
    [InlineData("")]
    public void NewNonScalpingSignal_InLiveMode_StaysLive(string state)
        => Assert.False(OrderRouter.RoutesToPaper(TradingMode.Live, Sig(state), scalpingPaperOnly: true));

    [Fact]
    public void KillSwitchOff_ScalpingFollowsGlobalMode()
        => Assert.False(OrderRouter.RoutesToPaper(TradingMode.Live, Sig("Scalping"), scalpingPaperOnly: false));

    [Fact]
    public void PaperMode_IsAlwaysPaper()
        => Assert.True(OrderRouter.RoutesToPaper(TradingMode.Paper, Sig("Trend"), scalpingPaperOnly: false));

    [Fact]
    public void ScalpingSignalOpenedLive_ClosesLive()
        => Assert.False(OrderRouter.RoutesToPaper(TradingMode.Live, Sig("Scalping", "GRW123456"), scalpingPaperOnly: true));

    [Fact]
    public void PaperIdSignal_ClosesPaper_EvenIfNotScalping()
        => Assert.True(OrderRouter.RoutesToPaper(TradingMode.Live, Sig("Trend", "PAPER-42"), scalpingPaperOnly: false));
}
