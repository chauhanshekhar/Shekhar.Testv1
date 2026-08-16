using Trading.Domain;
using Trading.SignalEngine.Services;

namespace Trading.Worker.SignalGates;

/// <summary>
/// EnableSignalScoring (default false, same per-symbol StrategySettings flag SignalEngineService
/// reads) changes how this gate treats Low-volatility regimes:
///   false -> unchanged: MarketRegimeService.IsTradeAllowed blocks both Low and Extreme volatility,
///            exactly as before scoring existed.
///   true  -> only Extreme volatility (genuinely unsafe — wide spreads, unpredictable moves) still
///            hard-blocks. Low volatility passes through instead of being suppressed outright; a
///            Low-volatility setup gets no ATR-quality bonus in SignalEngineService's score, so it
///            still needs the rest of the setup (Trend/EMA/RSI/body/etc.) to be strong enough to
///            clear the score threshold on its own — "reduce score / require stronger setup" per
///            spec, achieved without inventing a dedicated regime-score field. The expiry-morning
///            no-trade window is an independent time-based safety check and stays a hard block in
///            both modes.
/// </summary>
public class MarketRegimeGate : IPreSignalGate
{
    public Task<string?> BlockAsync(SignalContext ctx, MarketSnapshot market, CancellationToken ct)
    {
        var r = market.MarketRegime;
        var enableSignalScoring = ctx.StrategySettings.GetBool("EnableSignalScoring", ctx.Symbol, false);

        var allowed = enableSignalScoring
            ? IsTradeAllowedScored(r)
            : MarketRegimeService.IsTradeAllowed(r);

        if (allowed)
            return Task.FromResult<string?>(null);

        return Task.FromResult<string?>(
            $"Suppressed {ctx.Symbol} " +
            $"Trend={r.Trend} " +
            $"Vol={r.Volatility} " +
            $"ATR={r.Atr:F2} " +
            $"Expiry={r.IsExpiryDay} " +
            $"Gap={r.IsGapUp}/{r.IsGapDown}");
    }

    private static bool IsTradeAllowedScored(MarketRegime r)
    {
        if (r.Volatility == MarketVolatility.Extreme)
            return false;

        if (r.IsExpiryDay && IstClock.Now.TimeOfDay < new TimeSpan(9, 30, 0))
            return false;

        return true;
    }
}
