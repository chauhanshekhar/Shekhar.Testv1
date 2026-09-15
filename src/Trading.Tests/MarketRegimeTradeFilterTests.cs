using Trading.SignalEngine.Services;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Cover for MarketRegimeService.IsTradeAllowed — the volatility/expiry pre-gate.
///
/// Sideways is intentionally NOT rejected here. This method is only reached from
/// MarketRegimeGate, which runs before strategy selection, so a rule here would be global and
/// would also suppress HeroZero and Expiry. The Sideways restriction lives in
/// StrategySelectorService instead, where the chosen strategy is known and it applies to
/// Directional alone (see the 2026-08-18 Sideways/High incident referenced there).
/// </summary>
public class MarketRegimeTradeFilterTests
{
    private static MarketRegime Regime(MarketTrend trend, MarketVolatility vol, bool expiry = false) =>
        new(trend, vol, expiry, false, false, 0m);

    [Theory]
    [InlineData(MarketTrend.Uptrend)]
    [InlineData(MarketTrend.Downtrend)]
    [InlineData(MarketTrend.Sideways)]
    public void HealthyVolatility_IsAllowed_RegardlessOfTrend(MarketTrend trend)
    {
        // Sideways passes THIS gate by design — it is filtered later, per-strategy.
        Assert.True(MarketRegimeService.IsTradeAllowed(Regime(trend, MarketVolatility.High)));
    }

    [Theory]
    [InlineData(MarketVolatility.Low)]
    [InlineData(MarketVolatility.Extreme)]
    public void UnsafeVolatility_IsRejected_EvenWhenTrending(MarketVolatility vol)
    {
        Assert.False(MarketRegimeService.IsTradeAllowed(Regime(MarketTrend.Uptrend, vol)));
    }
}
