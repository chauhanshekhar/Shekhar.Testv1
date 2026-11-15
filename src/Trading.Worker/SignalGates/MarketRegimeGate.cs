using Trading.Domain;
using Trading.SignalEngine.Services;

namespace Trading.Worker.SignalGates;


public class MarketRegimeGate : IPreSignalGate
{
    public Task<string?> BlockAsync(
        SignalContext ctx,
        MarketSnapshot market,
        CancellationToken ct)
    {
        var r = market.MarketRegime;

        if (MarketRegimeService.IsTradeAllowed(r))
            return Task.FromResult<string?>(null);

        string reason = r.Volatility == MarketVolatility.Low
            ? "Low volatility"
            : r.IsExpiryDay &&
              IstClock.Now.TimeOfDay < new TimeSpan(9, 30, 0)
                ? "Expiry opening restriction"
                : "Market regime restriction";

        return Task.FromResult<string?>(
            $"Suppressed {ctx.Symbol}: {reason} | " +
            $"Trend={r.Trend} " +
            $"Vol={r.Volatility} " +
            $"ATR={r.Atr:F2} " +
            $"Expiry={r.IsExpiryDay} " +
            $"Gap={r.IsGapUp}/{r.IsGapDown}");
    }
}
