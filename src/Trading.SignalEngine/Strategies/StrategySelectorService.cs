using Trading.SignalEngine.Services;

namespace Trading.SignalEngine.Strategies;

/// <summary>
/// The "one brain" — pure decision function, no dependencies, over a StrategyContext snapshot.
/// Priority: Event/News (block) > Expiry > Trending market (Directional) > strong Hero Zero opportunity.
/// </summary>
public class StrategySelectorService
{
    private const decimal HeroZeroScoreThreshold = 90m;

    public StrategyDecision Select(StrategyContext ctx)
    {
        if (ctx.IsHighImpactEvent)
        {
            return new StrategyDecision
            {
                Strategy = SelectedStrategy.None,
                Priority = 1,
                Reason = $"High-impact news event on {ctx.Symbol} — blocked, waiting for market reaction"
            };
        }

        if (ctx.IsExpiryDay)
        {
            return new StrategyDecision
            {
                Strategy = SelectedStrategy.Expiry,
                Priority = 2,
                Reason = $"{ctx.Symbol} is expiry day"
            };
        }

        if (ctx.Regime != null && ctx.Regime.Trend != MarketTrend.Sideways)
        {
            return new StrategyDecision
            {
                Strategy = SelectedStrategy.Directional,
                Priority = 3,
                Reason = $"{ctx.Symbol} trending ({ctx.Regime.Trend})"
            };
        }

        if (ctx.HeroZeroTopScore > HeroZeroScoreThreshold)
        {
            return new StrategyDecision
            {
                Strategy = SelectedStrategy.HeroZero,
                Priority = 4,
                Reason = $"Sideways market but Hero Zero score {ctx.HeroZeroTopScore:F0} > {HeroZeroScoreThreshold:F0}"
            };
        }

        return new StrategyDecision
        {
            Strategy = SelectedStrategy.None,
            Priority = 0,
            Reason = $"No qualifying strategy for {ctx.Symbol} — sideways, Hero Zero score {ctx.HeroZeroTopScore:F0}"
        };
    }
}
