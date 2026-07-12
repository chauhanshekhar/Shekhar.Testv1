using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.SignalEngine.Scoring.Services;

/// <summary>
/// Max 10 — reuses the same TradingSettings.Liquidity* dictionaries already read by
/// SignalEngineService.IsLiquid, instead of inventing new thresholds.
/// </summary>
public class LiquidityScoreService
{
    public decimal Score(string symbol, OptionChainData option, TradingSettings settings)
    {
        var minVolume = settings.LiquidityMinVolume.GetValueOrDefault(symbol, 50_000);
        var minOi = settings.LiquidityMinOI.GetValueOrDefault(symbol, 10_000);
        var minLtp = settings.LiquidityMinLtp.GetValueOrDefault(symbol, 15m);
        var maxLtp = settings.LiquidityMaxLtp.GetValueOrDefault(symbol, 300m);

        var withinLtpBand = option.Ltp >= minLtp && option.Ltp <= maxLtp;

        if (!withinLtpBand || option.Volume < minVolume || option.OpenInterest < minOi)
            return 0m; // Poor

        var veryLiquid =
            option.Volume >= minVolume * 1.5m &&
            option.OpenInterest >= minOi * 1.5m;

        return veryLiquid ? 10m : 5m; // Very Liquid : Average
    }
}
