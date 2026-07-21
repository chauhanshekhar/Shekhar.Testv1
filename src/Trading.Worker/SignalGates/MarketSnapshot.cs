using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Services;

namespace Trading.Worker.SignalGates;

public record MarketSnapshot(
    List<Candle>                            Candles1m,
    List<Candle>                            Candles5m,
    List<OptionChainData>                   OptionChain,
    Dictionary<(decimal, string), long>     PrevOiByKey,
    MarketRegime                            MarketRegime,
    decimal Ema20,          // 1m — entry timing
    decimal Ema50,          // 1m — entry timing
    decimal Ema20_5m,       // 5m — trend filter only
    decimal Ema50_5m,       // 5m — trend filter only
    decimal Rsi,            // 1m
    decimal AtrPct,         // 1m
    decimal AtrRaw,         // 1m
    decimal Ltp,
    decimal YesterdayClose,
    decimal CurrentVolume,
    decimal AvgVolume20,
    decimal PriceChangePct,
    decimal Vwap,
    decimal PcrValue,
    string  StrategyHint,
    EconomicEvent? NewsEvent,
    TimeSpan LastTickAge,
    TimeSpan LastOptionChainAge,
    Candle? LastClosed5mCandle)
{
    /// <summary>
    /// Audit-only diagnostic row for this cycle — populated here, finished (MarketState/
    /// StrategySelected/Confidence/BlockedBy/BlockedReason) and saved once by SignalWorker.
    /// Never read by trading logic.
    /// </summary>
    public IndicatorSnapshot? Audit { get; init; }
}

public static class MarketSnapshotBuilder
{
    private const int MinCandles1m = 50;
    public static async Task<MarketSnapshot?> BuildAsync(SignalContext ctx)
    {
        var sym1m = $"{ctx.Symbol}_1m";
        var sym5m = $"{ctx.Symbol}_5m";

        var todayUtcStart = TradingConstants.TodayUtcStart();

        // ---------------------------------------------------------------------
        // Indicator candles (1m)
        // Rolling history by candle count, not a fixed calendar-day lookback —
        // a weekend/holiday gap means "yesterday" can have zero candles, so
        // AddDays(-1) is not reliable. EMA/RSI/ATR are stable from market open.
        // Exclude the still-forming 1m bar too — otherwise a mid-candle spike can fire
        // a signal off a price that reverses before that candle (or the 5m one) even closes.
        // ---------------------------------------------------------------------
        var recent1m = await ctx.CandleRepo.FindAsync(
            c => c.Symbol == sym1m,
            q => q.OrderByDescending(c => c.CandleTime).Take(260)); // buffer above 200 to survive excluding the forming bar

        var indicatorCandles = recent1m
            .OrderBy(c => c.CandleTime)
            .Where(c => c.CandleTime.AddMinutes(1) <= IstClock.Now)
            .TakeLast(200)
            .ToList();

        // ---------------------------------------------------------------------
        // Today's candles
        // Used only for intraday calculations.
        // ---------------------------------------------------------------------
        var todayCandles = indicatorCandles
            .Where(c => c.CandleTime >= todayUtcStart)
            .ToList();

        if (indicatorCandles.Count < MinCandles1m || todayCandles.Count == 0)
            return null;

        // ── Last tick age ─────────────────────────────────────────────────────
        var tickCutoff = DateTime.UtcNow.AddMinutes(-10);
        var recentTicks = await ctx.TickRepo.FindAsync(
            t => t.Symbol == ctx.Symbol && t.TickTime >= tickCutoff);
        var latestTickTime = recentTicks.Count > 0
            ? recentTicks.Max(t => t.TickTime)
            : (DateTime?)null;
        var lastTickAge = latestTickTime.HasValue
            ? IstClock.Now - latestTickTime.Value.ToLocalTime()
            : TimeSpan.MaxValue;

        var yesterdayClose = (await ctx.CandleRepo.FindAsync(
                c => c.Symbol == sym1m && c.CandleTime < todayUtcStart,
                q => q.OrderByDescending(c => c.CandleTime).Take(1)))
            .FirstOrDefault()?.Close ?? 0m;

        // ── 5m candles — trend filter only ───────────────────────────────────
        // Exclude the still-forming bar: strategy fires only on a fully closed 5m candle,
        // otherwise EMA/volume react to a partial bar and can whipsaw on a false breakout.
        // Indicator candles: rolling history (not just today) so EMA20_5m/EMA50_5m are
        // stable from market open, mirroring the 1m indicatorCandles pattern above.
        var recent5m = await ctx.CandleRepo.FindAsync(
            c => c.Symbol == sym5m,
            q => q.OrderByDescending(c => c.CandleTime).Take(130)); // buffer above 100 to survive excluding the forming bar

        var indicatorCandles5m = recent5m
            .OrderBy(c => c.CandleTime)
            .Where(c => c.CandleTime.AddMinutes(5) <= IstClock.Now)
            .TakeLast(100)
            .ToList();

        // Today's 5m candles — used only for volume; must not blend in yesterday's volume.
        var todayCandles5m = indicatorCandles5m
            .Where(c => c.CandleTime >= todayUtcStart)
            .ToList();

        // ── 1m indicators — entry signals ─────────────────────────────────────
        var closes1m = indicatorCandles.Select(x => x.Close).ToList();

        var ltp = indicatorCandles.Last().Close;
        var ema20 = ctx.Ema.GetEma(closes1m, 20);
        var ema50 = ctx.Ema.GetEma(closes1m, 50);
        var rsi = ctx.Rsi.GetRsi(closes1m);
        var atrRaw = ctx.Atr.Calculate(indicatorCandles, 14);

        var atrPct = ltp > 0
            ? atrRaw / ltp * 100m
            : 0m;

        // ── 5m EMA — trend filter (0 when not enough candles yet) ────────────
        decimal ema20_5m = 0m, ema50_5m = 0m;
        if (indicatorCandles5m.Count >= 20)
        {
            var closes5m = indicatorCandles5m.Select(x => x.Close).ToList();
            ema20_5m = ctx.Ema.GetEma(closes5m, 20);
            ema50_5m = ctx.Ema.GetEma(closes5m, 50);
        }

        // ── Volume from today's 5m candles only ───────────────────────────────
        decimal currentVolume, avgVolume20;
        if (todayCandles5m.Count >= 2)
        {
            currentVolume = (decimal)todayCandles5m.Last().Volume;
            avgVolume20 = (decimal)todayCandles5m.SkipLast(1).Average(c => c.Volume);
            if (avgVolume20 <= 0) avgVolume20 = 1m;
        }
        else
        {
            currentVolume = 0m;
            avgVolume20 = 1m;
        }

        var dayOpen = todayCandles.First().Open;
        var priceChangePct = dayOpen > 0 ? (ltp - dayOpen) / dayOpen * 100m : 0m;

        // ── VWAP — cumulative from today's session open, typical price weighted by volume ────
        var cumulativePriceVolume = todayCandles.Sum(c => (c.High + c.Low + c.Close) / 3m * c.Volume);
        var cumulativeVolume = todayCandles.Sum(c => c.Volume);
        var vwap = cumulativeVolume > 0 ? cumulativePriceVolume / cumulativeVolume : ltp;

        // ── Option chain ──────────────────────────────────────────────────────
        var chainCutoff = IstClock.Now.AddMinutes(-30);
        var fullChainRows = await ctx.OptionRepo.FindAsync(
            o => o.Symbol == ctx.Symbol && o.CreatedOn >= chainCutoff);

        var latestRow = fullChainRows.OrderByDescending(o => o.CreatedOn).FirstOrDefault();
        var latestSnapshotId = latestRow?.SnapshotId;

        var lastOptionChainAge = latestRow is not null
            ? IstClock.Now - latestRow.CreatedOn
            : TimeSpan.MaxValue;

        var optionChain = string.IsNullOrEmpty(latestSnapshotId)
            ? []
            : fullChainRows.Where(o => o.SnapshotId == latestSnapshotId).ToList<OptionChainData>();

        var prevSnapshotId = fullChainRows
            .Where(o => o.SnapshotId != latestSnapshotId
                     && o.CreatedOn <= IstClock.Now.AddMinutes(-4))
            .OrderByDescending(o => o.CreatedOn)
            .Select(o => o.SnapshotId)
            .FirstOrDefault();

        var prevOiByKey = string.IsNullOrEmpty(prevSnapshotId)
            ? new Dictionary<(decimal, string), long>()
            : fullChainRows
                .Where(o => o.SnapshotId == prevSnapshotId)
                .ToDictionary(o => (o.StrikePrice, o.OptionType), o => o.OpenInterest);

        var pcrValue = optionChain.Any() ? ctx.Pcr.Calculate(optionChain) : 1m;
        var marketRegime = ctx.Regime.Classify(
    ctx.Symbol,
    todayCandles,
    ema20,
    ema50,
    yesterdayClose,
    ctx.Instruments.IsExpiryDay(ctx.Symbol));

        // ── Events ────────────────────────────────────────────────────────────
        var upcomingEvents = await ctx.EventRepo.FindAsync(
            e => e.EventTimeUtc >= IstClock.Now.AddHours(-2) &&
                 e.EventTimeUtc <= IstClock.Now.AddHours(1));

        var newsEvent = ctx.EventCalendar.GetFireableNewsEvent(upcomingEvents);
        var strategyHint = TradingConst.StrategyType.Auto;

        // ── Audit snapshot (Market Snapshot Audit feature) ─────────────────────
        // Debugging/analytics only — built from indicators already computed above,
        // no extra queries. Not saved here; SignalWorker finishes and persists it once.
        var volumeRatio = avgVolume20 > 0 ? currentVolume / avgVolume20 : 0m;
        var trendDirection = ema20 > ema50 ? "Bullish" : ema20 < ema50 ? "Bearish" : "Sideways";
        var audit = new IndicatorSnapshot
        {
            SnapshotTime = IstClock.Now,
            Symbol = ctx.Symbol,
            Ltp = ltp,
            DayOpen = dayOpen,
            YesterdayClose = yesterdayClose,
            Ema20 = ema20,
            Ema50 = ema50,
            Ema20_5m = ema20_5m,
            Ema50_5m = ema50_5m,
            Rsi = rsi,
            AtrRaw = atrRaw,
            AtrPct = atrPct,
            Pcr = pcrValue,
            Vwap = vwap,
            CurrentVolume = currentVolume,
            AvgVolume20 = avgVolume20,
            VolumeRatio = volumeRatio,
            PriceChangePct = priceChangePct,
            TrendDirection = trendDirection,
            MarketRegime = $"{marketRegime.Trend}/{marketRegime.Volatility}",
        };

        return new MarketSnapshot(
            Candles1m: indicatorCandles,
            Candles5m: indicatorCandles5m,
            OptionChain: optionChain,
            PrevOiByKey: prevOiByKey,
            MarketRegime: marketRegime,
            Ema20: ema20,
            Ema50: ema50,
            Ema20_5m: ema20_5m,
            Ema50_5m: ema50_5m,
            Rsi: rsi,
            AtrPct: atrPct,
            AtrRaw: atrRaw,
            Ltp: ltp,
            YesterdayClose: yesterdayClose,
            CurrentVolume: currentVolume,
            AvgVolume20: avgVolume20,
            PriceChangePct: priceChangePct,
            Vwap: vwap,
            PcrValue: pcrValue,
            StrategyHint: strategyHint,
            NewsEvent: newsEvent,
            LastTickAge: lastTickAge,
            LastOptionChainAge: lastOptionChainAge,
            LastClosed5mCandle: todayCandles5m.Count > 0 ? todayCandles5m[^1] : null)
        {
            Audit = audit
        };
    }
    //public static async Task<MarketSnapshot?> BuildAsync(SignalContext ctx)
    //{
    //    var sym1m = $"{ctx.Symbol}_1m";
    //    var sym5m = $"{ctx.Symbol}_5m";

    //    var todayUtcStart = TradingConstants.TodayUtcStart();

    //    var allSymbolCandles = await ctx.CandleRepo.FindAsync(
    //        c => c.Symbol == sym1m && c.CandleTime >= todayUtcStart.AddDays(-1));

    //    // Exclude the still-forming 1m bar too — otherwise a mid-candle spike can fire
    //    // a signal off a price that reverses before that candle (or the 5m one) even closes.
    //    var candles1m = allSymbolCandles
    //        .Where(c => c.CandleTime >= todayUtcStart)
    //        .OrderBy(c => c.CandleTime)
    //        .Where(c => c.CandleTime.AddMinutes(1) <= IstClock.Now)
    //        .TakeLast(100)
    //        .ToList();

    //    if (candles1m.Count < MinCandles1m)
    //        return null;

    //    // ── Last tick age ─────────────────────────────────────────────────────
    //    var tickCutoff     = DateTime.UtcNow.AddMinutes(-10);
    //    var recentTicks    = await ctx.TickRepo.FindAsync(
    //        t => t.Symbol == ctx.Symbol && t.TickTime >= tickCutoff);
    //    var latestTickTime = recentTicks.Count > 0
    //        ? recentTicks.Max(t => t.TickTime)
    //        : (DateTime?)null;
    //    var lastTickAge    = latestTickTime.HasValue
    //        ? IstClock.Now - latestTickTime.Value.ToLocalTime()
    //        : TimeSpan.MaxValue;

    //    var yesterdayClose = allSymbolCandles
    //        .Where(c => c.CandleTime < todayUtcStart)
    //        .OrderByDescending(c => c.CandleTime)
    //        .FirstOrDefault()?.Close ?? 0m;

    //    // ── 5m candles — trend filter only ───────────────────────────────────
    //    // Exclude the still-forming bar: strategy fires only on a fully closed 5m candle,
    //    // otherwise EMA/volume react to a partial bar and can whipsaw on a false breakout.
    //    var candles5m = (await ctx.CandleRepo.FindAsync(
    //            c => c.Symbol == sym5m && c.CandleTime >= todayUtcStart))
    //        .OrderBy(c => c.CandleTime)
    //        .Where(c => c.CandleTime.AddMinutes(5) <= IstClock.Now)
    //        .TakeLast(60)
    //        .ToList();

    //    // ── 1m indicators — entry signals ─────────────────────────────────────
    //    var closes1m  = candles1m.Select(x => x.Close).ToList();
    //    var ltp       = candles1m.Last().Close;
    //    var ema20     = ctx.Ema.GetEma(closes1m, 20);
    //    var ema50     = ctx.Ema.GetEma(closes1m, 50);
    //    var rsi       = ctx.Rsi.GetRsi(closes1m);
    //    var atrRaw    = ctx.Atr.Calculate(candles1m, 14);
    //    var atrPct    = ltp > 0 ? atrRaw / ltp * 100m : 0m;

    //    // ── 5m EMA — trend filter (0 when not enough candles yet) ────────────
    //    decimal ema20_5m = 0m, ema50_5m = 0m;
    //    if (candles5m.Count >= 20)
    //    {
    //        var closes5m = candles5m.Select(x => x.Close).ToList();
    //        ema20_5m = ctx.Ema.GetEma(closes5m, 20);
    //        ema50_5m = ctx.Ema.GetEma(closes5m, 50);
    //    }

    //    // ── Volume from 5m ────────────────────────────────────────────────────
    //    decimal currentVolume, avgVolume20;
    //    if (candles5m.Count >= 2)
    //    {
    //        currentVolume = (decimal)candles5m.Last().Volume;
    //        avgVolume20   = (decimal)candles5m.SkipLast(1).Average(c => c.Volume);
    //        if (avgVolume20 <= 0) avgVolume20 = 1m;
    //    }
    //    else
    //    {
    //        currentVolume = 0m;
    //        avgVolume20   = 1m;
    //    }

    //    var dayOpen        = candles1m.First().Open;
    //    var priceChangePct = dayOpen > 0 ? (ltp - dayOpen) / dayOpen * 100m : 0m;

    //    // ── Option chain ──────────────────────────────────────────────────────
    //    var chainCutoff   = IstClock.Now.AddMinutes(-30);
    //    var fullChainRows = await ctx.OptionRepo.FindAsync(
    //        o => o.Symbol == ctx.Symbol && o.CreatedOn >= chainCutoff);

    //    var latestRow        = fullChainRows.OrderByDescending(o => o.CreatedOn).FirstOrDefault();
    //    var latestSnapshotId = latestRow?.SnapshotId;

    //    var lastOptionChainAge = latestRow is not null
    //        ? IstClock.Now - latestRow.CreatedOn
    //        : TimeSpan.MaxValue;

    //    var optionChain = string.IsNullOrEmpty(latestSnapshotId)
    //        ? []
    //        : fullChainRows.Where(o => o.SnapshotId == latestSnapshotId).ToList<OptionChainData>();

    //    var prevSnapshotId = fullChainRows
    //        .Where(o => o.SnapshotId != latestSnapshotId
    //                 && o.CreatedOn  <= IstClock.Now.AddMinutes(-4))
    //        .OrderByDescending(o => o.CreatedOn)
    //        .Select(o => o.SnapshotId)
    //        .FirstOrDefault();

    //    var prevOiByKey = string.IsNullOrEmpty(prevSnapshotId)
    //        ? new Dictionary<(decimal, string), long>()
    //        : fullChainRows
    //            .Where(o => o.SnapshotId == prevSnapshotId)
    //            .ToDictionary(o => (o.StrikePrice, o.OptionType), o => o.OpenInterest);

    //    var pcrValue     = optionChain.Any() ? ctx.Pcr.Calculate(optionChain) : 1m;
    //    var marketRegime = ctx.Regime.Classify(
    //        ctx.Symbol, candles1m, ema20, ema50, yesterdayClose,
    //        ctx.Instruments.IsExpiryDay(ctx.Symbol));

    //    // ── Events ────────────────────────────────────────────────────────────
    //    var upcomingEvents = await ctx.EventRepo.FindAsync(
    //        e => e.EventTimeUtc >= IstClock.Now.AddHours(-2) &&
    //             e.EventTimeUtc <= IstClock.Now.AddHours(1));

    //    var newsEvent    = ctx.EventCalendar.GetFireableNewsEvent(upcomingEvents);
    //    var strategyHint = TradingConst.StrategyType.Auto;

    //    return new MarketSnapshot(
    //        Candles1m:          candles1m,
    //        Candles5m:          candles5m,
    //        OptionChain:        optionChain,
    //        PrevOiByKey:        prevOiByKey,
    //        MarketRegime:       marketRegime,
    //        Ema20:              ema20,
    //        Ema50:              ema50,
    //        Ema20_5m:           ema20_5m,
    //        Ema50_5m:           ema50_5m,
    //        Rsi:                rsi,
    //        AtrPct:             atrPct,
    //        AtrRaw:             atrRaw,
    //        Ltp:                ltp,
    //        YesterdayClose:     yesterdayClose,
    //        CurrentVolume:      currentVolume,
    //        AvgVolume20:        avgVolume20,
    //        PriceChangePct:     priceChangePct,
    //        PcrValue:           pcrValue,
    //        StrategyHint:       strategyHint,
    //        NewsEvent:          newsEvent,
    //        LastTickAge:        lastTickAge,
    //        LastOptionChainAge: lastOptionChainAge,
    //        LastClosed5mCandle: candles5m.Count > 0 ? candles5m[^1] : null);
    //}
}
