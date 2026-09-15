using Trading.Domain;
using Trading.SignalEngine.Services;

namespace Trading.Worker.SignalGates;

public class MarketRegimeGate : IPreSignalGate
{
    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var r = market.MarketRegime;

        if (MarketRegimeService.IsTradeAllowed(r))
            return Task.FromResult<string?>(null);

        return Task.FromResult<string?>(
            $"Suppressed {ctx.Symbol} " +
            $"Trend={r.Trend} " +
            $"Vol={r.Volatility} " +
            $"ATR={r.Atr:F2} " +
            $"Expiry={r.IsExpiryDay} " +
            $"Gap={r.IsGapUp}/{r.IsGapDown}");
    }
}
