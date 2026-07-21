using Trading.Domain;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;

namespace Trading.SignalEngine.Strategies;

/// <summary>
/// The "one brain" — decision function over a StrategyContext snapshot. Drives strategy choice off
/// MarketState (from MarketClassificationService) rather than raw trend/HeroZero-score checks — see
/// StrategyContext.MarketState / MarketStateReason. For Trend/StrongTrend, Directional and HeroZero
/// are a genuine historical-win-rate contest (StrategyPerformanceService) once both sides have enough
/// trades (TradingSettings.MinTradesForHistoricalWeight) — otherwise falls back to the fixed default
/// (Directional), same as before this override existed.
/// </summary>
public class StrategySelectorService
{
    private readonly StrategyPerformanceService _performance;
    private readonly SystemSettingsService _systemSettings;

    public StrategySelectorService(StrategyPerformanceService performance, SystemSettingsService systemSettings)
    {
        _performance = performance;
        _systemSettings = systemSettings;
    }

    public async Task<StrategyDecision> Select(StrategyContext ctx)
    {
        // Defensive first check — kept even though News below is now MarketClassificationService's
        // job too, per "never remove safety checks".
        if (ctx.IsHighImpactEvent)
        {
            return new StrategyDecision
            {
                Strategy = SelectedStrategy.None,
                Priority = 1,
                MarketState = ctx.MarketState,
                Reason = $"High-impact news event on {ctx.Symbol} — blocked, waiting for market reaction"
            };
        }

        if (ctx.MarketState == MarketState.News)
        {
            return new StrategyDecision
            {
                Strategy = SelectedStrategy.None,
                Priority = 1,
                MarketState = ctx.MarketState,
                Reason = ctx.MarketStateReason
            };
        }

        // Expiry is an attribute of the market, not a state that overrides classification — the
        // time-of-day dispatch applies on top of whatever MarketState was actually classified, so
        // the log/persisted MarketState stays the real one (e.g. "Trend") instead of always "Expiry".
        if (ctx.IsExpiryDay)
        {
            var settings = await _systemSettings.GetAsync();
            var now = IstClock.Now.TimeOfDay;

            // First 30 minutes of expiry day — wide option spreads, gap traps, IV still settling,
            // fake breakouts. Distinct from (and ahead of) MarketRegimeGate's gap-trap window, which
            // only fires on an actual gap; this blocks the whole opening window on expiry regardless.
            if (now < settings.ExpiryMorningNoTradeEnd)
            {
                return new StrategyDecision
                {
                    Strategy = SelectedStrategy.None,
                    Priority = 2,
                    MarketState = ctx.MarketState,
                    Reason = $"{ctx.MarketStateReason} [Expiry] — before {settings.ExpiryMorningNoTradeEnd:hh\\:mm} IST, opening window blocked"
                };
            }

            // Reuses ExpiryGammaBlastStartTime (the same 14:00 boundary ExpirySignalEngine's own
            // GammaBlast mode switches on) rather than a second, separately-tuned cutoff.
            var beforeCutoff = now < settings.ExpiryGammaBlastStartTime;
            return new StrategyDecision
            {
                Strategy = beforeCutoff ? SelectedStrategy.Directional : SelectedStrategy.HeroZero,
                Priority = 2,
                MarketState = ctx.MarketState,
                Reason = $"{ctx.MarketStateReason} [Expiry] — {(beforeCutoff ? "09:45-14:00" : "at/after 14:00")} IST"
            };
        }

        switch (ctx.MarketState)
        {
            case MarketState.StrongTrend:
            case MarketState.Trend:
                return await SelectTrendStrategy(ctx);

            // No dedicated Scalping engine yet — Directional is the closest existing fit.
            case MarketState.Breakout:
            case MarketState.Breakdown:
            case MarketState.Scalping:
                return new StrategyDecision
                {
                    Strategy = SelectedStrategy.Directional,
                    Priority = 4,
                    MarketState = ctx.MarketState,
                    Reason = $"{ctx.MarketStateReason} — running Directional engine (no dedicated Scalping engine yet)"
                };

            // Reversal is higher-risk on an unvalidated classifier — suppressed until a
            // dedicated reversal strategy exists.
            case MarketState.Reversal:
            case MarketState.Range:
            case MarketState.NoTrade:
            default:
                return new StrategyDecision
                {
                    Strategy = SelectedStrategy.None,
                    Priority = 0,
                    MarketState = ctx.MarketState,
                    Reason = ctx.MarketStateReason
                };
        }
    }

    // Directional-vs-HeroZero, decided by historical win rate once both sides have enough trades
    // in this exact (MarketState, Expiry, Hour) bucket; otherwise the fixed default (Directional).
    private async Task<StrategyDecision> SelectTrendStrategy(StrategyContext ctx)
    {
        var hour = IstClock.Now.Hour;
        var marketState = ctx.MarketState.ToString();

        var directional = await _performance.GetWinRateAsync("Directional", marketState, ctx.IsExpiryDay, hour);
        var heroZero = await _performance.GetWinRateAsync("HeroZero", marketState, ctx.IsExpiryDay, hour);

        if (directional != null && heroZero != null && heroZero.Value.WinRate > directional.Value.WinRate)
        {
            return new StrategyDecision
            {
                Strategy = SelectedStrategy.HeroZero,
                Priority = 3,
                MarketState = ctx.MarketState,
                HistoricalWinRate = heroZero.Value.WinRate,
                HistoricalTrades = heroZero.Value.Trades,
                Reason = $"{ctx.MarketStateReason} — HeroZero historical win rate {heroZero.Value.WinRate:F0}% " +
                         $"(n={heroZero.Value.Trades}) beats Directional {directional.Value.WinRate:F0}% (n={directional.Value.Trades})"
            };
        }

        return new StrategyDecision
        {
            Strategy = SelectedStrategy.Directional,
            Priority = 3,
            MarketState = ctx.MarketState,
            HistoricalWinRate = directional?.WinRate,
            HistoricalTrades = directional?.Trades,
            Reason = directional != null
                ? $"{ctx.MarketStateReason} — Directional historical win rate {directional.Value.WinRate:F0}% (n={directional.Value.Trades})"
                : ctx.MarketStateReason
        };
    }
}
