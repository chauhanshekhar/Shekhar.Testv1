using Microsoft.Extensions.Logging;
using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Scoring.Models;
using Trading.SignalEngine.Scoring.Ranking;
using Trading.SignalEngine.Scoring.Services;

namespace Trading.SignalEngine.Services;

/// <summary>
/// Hero Zero engine — scores every CE and PE in the chain, ranks each side, and trades
/// only when one side's Hero score dominates the other's (see StrikeRankingService).
/// Exit: 50% SL, 100% T1 (2x), 300% T2 (4x) — set in SignalBuilder.AsHeroZero().
/// </summary>
public class HeroZeroSignalEngine
{
    private readonly IInstrumentCache _instruments;
    private readonly SystemSettingsService _settingsService;
    private readonly HeroZeroScoreService _scoreService;
    private readonly StrikeRankingService _ranking;
    private readonly IRepository<HeroZeroScore> _scoreRepo;
    private readonly IUnitOfWork _uow;
    private readonly ILogger<HeroZeroSignalEngine> _logger;

    private static readonly TimeSpan NormalDayStart = new(9, 30, 0);
    private static readonly TimeSpan ExpiryGammaModeStart = new(11, 0, 0);
    private static readonly TimeSpan ExpiryLotteryModeStart = new(14, 30, 0);

    private const decimal LotteryMinPremium = 5m;
    private const decimal LotteryMaxPremium = 20m;
    private const decimal LotteryMinScore = 90m;

    public HeroZeroSignalEngine(
        IInstrumentCache instruments,
        SystemSettingsService settingsService,
        HeroZeroScoreService scoreService,
        StrikeRankingService ranking,
        IRepository<HeroZeroScore> scoreRepo,
        IUnitOfWork uow,
        ILogger<HeroZeroSignalEngine> logger)
    {
        _instruments = instruments;
        _settingsService = settingsService;
        _scoreService = scoreService;
        _ranking = ranking;
        _scoreRepo = scoreRepo;
        _uow = uow;
        _logger = logger;
    }

    public async Task<Signal?> Generate(
        string symbol,
        decimal ltp,
        decimal ema20,
        decimal ema50,
        decimal pcr,
        decimal rsi,
        decimal currentVolume,
        decimal avgVolume20,
        decimal priceChangePct,
        decimal ema20_5m,
        decimal ema50_5m,
        List<Candle> candles5m,
        bool isExpiryDay,
        List<OptionChainData> chain)
    {
        if (chain == null || chain.Count == 0) return null;

        var settings = await _settingsService.GetAsync();
        var now = IstClock.Now.TimeOfDay;

        var interval = (decimal)_instruments.GetStrikeInterval(symbol);
        var spot = chain.Where(x => x.UnderlyingLtp > 0).Select(x => x.UnderlyingLtp).FirstOrDefault();
        if (spot <= 0) spot = ltp;
        var atmStrike = Math.Round(spot / interval, MidpointRounding.AwayFromZero) * interval;

        decimal minPremium = settings.HeroZeroMinPremium;
        decimal maxPremium = settings.HeroZeroMaxPremium.GetValueOrDefault(symbol, 30m);
        decimal minScore = 85m;

        List<OptionChainData> universe;

        if (isExpiryDay)
        {
            if (now < ExpiryGammaModeStart)
            {
                // 09:15–11:00 — avoid cheap Hero Zero, ATM only.
                universe = chain.Where(o => o.StrikePrice == atmStrike).ToList();
            }
            else if (now >= ExpiryLotteryModeStart)
            {
                // After 14:30 — lottery mode: wider premium band, higher bar.
                minPremium = LotteryMinPremium;
                maxPremium = LotteryMaxPremium;
                minScore = LotteryMinScore;
                universe = chain;
            }
            else
            {
                // 11:00–14:30 — Gamma Mode: full chain, standard bar.
                universe = chain;
            }
        }
        else
        {
            if (now < NormalDayStart)
            {
                _logger.LogDebug("HeroZero: before {Start} — suppressed ({Symbol})", NormalDayStart, symbol);
                return null;
            }

            universe = chain;
        }

        var scoringChain = universe
            .Where(o => o.Ltp >= minPremium && o.Ltp <= maxPremium)
            .ToList();

        if (scoringChain.Count == 0)
        {
            _logger.LogDebug("HeroZero: no strikes in premium band [{Min},{Max}] for {Symbol}", minPremium, maxPremium, symbol);
            return null;
        }

        var (ceScores, peScores) = _scoreService.Score(
            symbol, scoringChain, ema20, ema50, ema20_5m, ema50_5m, priceChangePct, candles5m, settings);

        await PersistScoresAsync(ceScores, peScores);

        var result = _ranking.Rank(ceScores, peScores);

        if (result.Decision == HeroZeroDecision.NoTrade)
        {
            _logger.LogDebug("HeroZero: {Reason} ({Symbol})", result.Reason, symbol);
            return null;
        }

        var hero = result.Decision == HeroZeroDecision.BuyCe ? result.HeroCe : result.HeroPe;
        if (hero == null || hero.TotalScore < minScore)
        {
            _logger.LogDebug("HeroZero: Hero score {Score:F0} < {Min:F0} — suppressed ({Symbol})",
                hero?.TotalScore ?? 0m, minScore, symbol);
            return null;
        }

        var call = result.Decision == HeroZeroDecision.BuyCe;

        var optionSymbol = !string.IsNullOrWhiteSpace(hero.TradingSymbol)
            ? hero.TradingSymbol
            : $"{symbol}{IstClock.Now:yyMMM}{(int)hero.StrikePrice}{hero.OptionType}".ToUpper();

        _logger.LogInformation(
            "HeroZeroEngine: {Type} {Symbol} Strike:{Strike} Ltp:{Ltp} Score:{Score:F0} " +
            "(Trend:{Trend:F0} OI:{Oi:F0} Vol:{Vol:F0} Greeks:{Greeks:F0} Liquidity:{Liq:F0} Momentum:{Mom:F0}) — {Reason}",
            hero.OptionType, symbol, hero.StrikePrice, hero.Ltp, hero.TotalScore,
            hero.TrendScore, hero.OiScore, hero.VolumeScore, hero.GreeksScore, hero.LiquidityScore, hero.MomentumScore,
            result.Reason);

        var builder = new SignalBuilder(symbol);
        if (call) builder.CALL(); else builder.PUT();

        return builder
            .AtIndexPrice(ltp)
            .WithIndexStops(_instruments.GetIndexStopPoints(symbol))
            .ForOption(hero.StrikePrice, hero.OptionType, optionSymbol, hero.Ltp)
            .AsHeroZero() // overrides SL/T1/T2 to aggressive levels
            .OnExchange(_instruments.GetExchange(symbol))
            .WithLots(1) // always 1 lot — high risk premium
            .WithConfidence(hero.TotalScore)
            .Build();
    }

    /// <summary>Best-effort — a DB hiccup here must never block signal generation.</summary>
    private async Task PersistScoresAsync(List<StrikeScore> ceScores, List<StrikeScore> peScores)
    {
        try
        {
            var rows = ceScores.Concat(peScores).Select(s => new HeroZeroScore
            {
                CreatedOn = IstClock.Now,
                Symbol = s.Symbol,
                TradingSymbol = s.TradingSymbol,
                StrikePrice = s.StrikePrice,
                OptionType = s.OptionType,
                TrendScore = s.TrendScore,
                OiScore = s.OiScore,
                VolumeScore = s.VolumeScore,
                GreeksScore = s.GreeksScore,
                LiquidityScore = s.LiquidityScore,
                MomentumScore = s.MomentumScore,
                TotalScore = s.TotalScore
            }).ToList();

            if (rows.Count == 0) return;

            await _scoreRepo.AddRangeAsync(rows);
            await _uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HeroZero: failed to persist strike scores — continuing without persistence");
        }
    }
}
