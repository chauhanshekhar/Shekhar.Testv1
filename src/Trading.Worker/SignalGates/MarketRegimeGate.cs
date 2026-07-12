using Trading.Domain;
using Trading.SignalEngine.Services;

namespace Trading.Worker.SignalGates;

public class MarketRegimeGate : IPreSignalGate
{
    private static readonly TimeSpan GapTrapWindow = new(9, 45, 0);

    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var r = market.MarketRegime;

        // Block for first 15 min after a gap open — price action is chaotic until gap fills or extends.
        if ((r.IsGapUp || r.IsGapDown) && IstClock.Now.TimeOfDay < GapTrapWindow)
            return Task.FromResult<string?>(
                $"Gap open trap — signals suppressed until 09:45 ({ctx.Symbol} gap {(r.IsGapUp ? "UP" : "DOWN")})");

        if (MarketRegimeService.IsTradeAllowed(market.MarketRegime))
            return Task.FromResult<string?>(null);

        return Task.FromResult<string?>(
            $"Trade suppressed {ctx.Symbol} — Regime: {r.Trend}/{r.Volatility} " +
            $"Expiry:{r.IsExpiryDay} Gap:{r.IsGapUp}/{r.IsGapDown}");
    }
}
