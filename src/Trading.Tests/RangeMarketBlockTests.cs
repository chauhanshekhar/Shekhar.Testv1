using Trading.SignalEngine.Services;
using Trading.Worker;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// MarketState=Range is no-trade. The selector already returns "no strategy" for Range on normal days, but that still
/// reached the approved-rule fallback and was bypassed on expiry days, so StrategyManager stops the state outright.
/// </summary>
public class RangeMarketBlockTests
{
    [Fact]
    public void Range_IsBlocked_WhenSwitchOn()
        => Assert.True(StrategyManager.ShouldBlockRangeMarket(MarketState.Range, blockRange: true));

    [Theory]
    [InlineData(MarketState.Trend)]
    [InlineData(MarketState.StrongTrend)]
    [InlineData(MarketState.Breakout)]
    [InlineData(MarketState.Breakdown)]
    [InlineData(MarketState.Scalping)]
    public void OtherStates_AreNotBlockedByThisRule(MarketState state)
        => Assert.False(StrategyManager.ShouldBlockRangeMarket(state, blockRange: true));

    [Fact]
    public void Range_IsAllowed_WhenSwitchTurnedOff()
        => Assert.False(StrategyManager.ShouldBlockRangeMarket(MarketState.Range, blockRange: false));
}
