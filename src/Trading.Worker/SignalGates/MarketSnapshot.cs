using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Brokers.Groww;
using Trading.Infrastructure.Repositories;
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
    decimal Rsi,            // 1m — recorded (Signal.RsiAtEntry); no longer the entry check
    decimal? Rsi5m,         // 5m — the CALL/PUT entry RSI band is checked against this (Signal.Rsi5mAtEntry); null until 15 closed 5m bars exist
    decimal AtrPct,         // 1m
    decimal AtrRaw,         // 1m
    decimal Ltp,
    decimal YesterdayClose,
    decimal CurrentVolume,
    decimal AvgVolume20,
    decimal PriceChangePct,
    // ── Day context, for the CALL day filter ─────────────────────────────────────────────────
    // Sourced from the latest MarketTick (the broker's own session figures), falling back to the
    // candle series only when no tick is available.
    //
    // DayChangePct is measured against YESTERDAY'S CLOSE and is NOT the same quantity as
    // PriceChangePct above, which is measured against today's OPEN. A gap-down session can show a
    // strongly negative DayChangePct while PriceChangePct is positive; the two must not be
    // conflated. On 2026-09-02 BANKNIFTY opened at 57006 against a 57410 previous close — the two
    // measures disagreed in sign for the entire session.
    decimal DayOpen,
    decimal DayLow,
    decimal DayHigh,
    decimal DayChangePct,
    decimal? Vwap,
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

    /// <summary>Last closed 1-minute candle — entry trigger candidate (see UseOneMinuteEntryCandle).</summary>
    public Candle? LastClosed1mCandle { get; init; }

    /// <summary>1m EMA9 / EMA21 (0 when fewer than 9 / 21 closed bars). Read only by FastEmaGate.</summary>
    public decimal Ema9 { get; init; }
    public decimal Ema21 { get; init; }

    /// <summary>
    /// 5m ATR(14), Wilder, over the same closed 5m bars as Ema20_5m/Ema50_5m. 0 = missing (fewer than 15 closed bars, or the newest
    /// closed bar is stale). Read only by EarlyTrendGate's soft rule, which treats 0 as "gate unchanged".
    /// </summary>
    public decimal Atr5m { get; init; }
}

public static class MarketSnapshotBuilder
{
    private const int MinCandles1m = 50;

    /// <summary>
    /// A bar counts as CLOSED only once its full duration has elapsed. The still-forming candle is
    /// excluded from every series this builder produces, so no trade decision — 1-minute entry
    /// trigger or 5-minute confirmation — can ever be influenced by a partial bar.
    ///
    /// <paramref name="candleTime"/> and <paramref name="now"/> must be on the same clock; the
    /// builder calls it through <see cref="IsClosedNow"/>.
    /// </summary>
    internal static bool IsClosedBar(DateTime candleTime, int barMinutes, DateTime now) =>
        candleTime.AddMinutes(barMinutes) <= now;

    /// <summary>
    /// <see cref="IsClosedBar"/> on the clock Candle.CandleTime is stored in: UTC
    /// (GrowwHistoricalDataService stores Groww's epoch as .UtcDateTime). Until 2026-09-30 the
    /// builder compared against IstClock.Now, 5h30m ahead, so every bar - the forming one
    /// included - counted as closed.
    /// </summary>
    internal static bool IsClosedNow(DateTime candleTimeUtc, int barMinutes) =>
        IsClosedBar(candleTimeUtc, barMinutes, DateTime.UtcNow);

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
            .Where(c => IsClosedNow(c.CandleTime, 1))
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
            .Where(c => IsClosedNow(c.CandleTime, 5))
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
        var ema9 = ctx.Ema.GetEma(closes1m, 9);     // audit only
        var ema21 = ctx.Ema.GetEma(closes1m, 21);   // audit only
        var rsi = ctx.Rsi.GetRsi(closes1m);
        // Use only today's candles for ATR
        var atrSource = todayCandles.Count >= 14
            ? todayCandles
            : indicatorCandles.TakeLast(14).ToList(); // fallback near market open

        var atrRaw = ctx.Atr.Calculate(atrSource, 14);

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

        // ── 5m RSI — the CALL/PUT entry RSI band is checked against this ─────
        // SignalEngineService condition 5 compares Call/PutRsiFloor..Ceiling with this value
        // (stored as Signal.Rsi5mAtEntry); the 1m RSI above is recorded next to it. The two
        // legitimately disagree — a 1m RSI(14) is far spikier and routinely reads 80+ on a move
        // that reads ~63 on 5m.
        //
        // NULL rather than a stand-in when the series is too short: RsiService returns a neutral
        // 50 on insufficient data, and persisting that would look like a real reading. RSI(14)
        // needs 15 closed 5m bars, taken from rolling history (not only today's bars).
        decimal? rsi5m = null;
        if (indicatorCandles5m.Count > 14)
            rsi5m = ctx.Rsi.GetRsi(indicatorCandles5m.Select(x => x.Close).ToList());

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

        // ── Session values for the CALL day filter, from the broker's own tick ───────────────
        // MarketTick carries authoritative session figures that beat anything derived from our
        // candles: Open/High/Low are the SESSION's extremes and Close is the PREVIOUS session's
        // official close. Verified against the candle series on 2026-09-02 — tick.Close matched
        // the prior day's last candle close exactly on 8 of 11 symbol-days, and the three
        // mismatches (-5.40, +49.69, -47.15) are the candle series missing the official closing
        // print, so the tick is the more accurate of the two. tick.Low also sits slightly BELOW
        // the candle low, because ticks capture extremes that no recorded minute bar contains.
        //
        // priceChangePct above is deliberately LEFT on its candle-derived dayOpen: it feeds the
        // PUT chain and the IndicatorSnapshot audit column, and this change is CALL-only.
        var latestTick = recentTicks.Count > 0
            ? recentTicks.OrderByDescending(t => t.TickTime).First()
            : null;

        var sessionOpen = latestTick is { Open: > 0 } ? latestTick.Open : dayOpen;
        var sessionLow = latestTick is { Low: > 0 }
            ? latestTick.Low
            : (todayCandles.Count > 0 ? todayCandles.Min(c => c.Low) : 0m);
        var sessionHigh = latestTick is { High: > 0 }
            ? latestTick.High
            : (todayCandles.Count > 0 ? todayCandles.Max(c => c.High) : 0m);
        var prevClose = latestTick is { Close: > 0 } ? latestTick.Close : yesterdayClose;

        var dayChangePct = prevClose > 0 ? (ltp - prevClose) / prevClose * 100m : 0m;

        // ── VWAP — cumulative from today's session open, typical price weighted by volume ────
        var cumulativePriceVolume = todayCandles.Sum(c => (c.High + c.Low + c.Close) / 3m * c.Volume);
        var cumulativeVolume = todayCandles.Sum(c => c.Volume);
        // NULL when volume is unavailable, never a fabricated stand-in. Groww does not publish
        // index candle volume -- every Candle row is Volume=0 -- so cumulativeVolume is always 0
        // and VWAP is not computable. The old fallback substituted `ltp`, which made the audit
        // column look like real VWAP data and would silently turn any future "price vs VWAP"
        // check into "price vs price" (never true). A null forces callers to handle the gap.
        decimal? vwap = cumulativeVolume > 0 ? cumulativePriceVolume / cumulativeVolume : null;

        // ── Volume / VWAP from the near-month index future (2026-10-08) ─────────────────────────
        // HistoricalDataWorker stores the future's 1m bars as {SYMBOL}_FUT_1m. When they exist they replace the
        // index-candle values above (always 0 / null). VWAP is shifted into index points (FuturesFlow). No entry
        // condition reads volume or VWAP yet - this only fills the inputs and the IndicatorSnapshots.Vwap column.
        var symFut = GrowwFutures.CandleSymbol(ctx.Symbol);
        var futureBars = (await ctx.CandleRepo.FindAsync(c => c.Symbol == symFut && c.CandleTime >= todayUtcStart))
            .Where(c => IsClosedNow(c.CandleTime, 1))
            .OrderBy(c => c.CandleTime)
            .ToList();
        if (FuturesFlow.From(futureBars, ltp) is { } flow)
        {
            vwap = flow.Vwap;
            currentVolume = flow.CurrentVolume;
            avgVolume20 = flow.AvgVolume20;
        }

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
        // Full-chain PCR is stored separately by OptionChainWorker.
        // Never substitute ATM PCR or a neutral value when full-chain data is missing.
        decimal? pcrFullChainValue = null;

        var indicatorRepo =
            ctx.ServiceProvider?.GetRequiredService<IRepository<SymbolIndicator>>();

        if (indicatorRepo is not null)
        {
            var fullChainIndicator = await indicatorRepo.FirstOrDefaultAsync(
                i => i.Symbol == ctx.Symbol &&
                     i.IndicatorName == TradingConst.Indicator.PcrFullChain);

            if (fullChainIndicator is not null &&
                fullChainIndicator.UpdatedOn >= IstClock.Now.AddMinutes(-2))
            {
                pcrFullChainValue = fullChainIndicator.Value;
            }
        }
        // Change 2: seed the regime from ROLLING history (indicatorCandles), not today-only.
        // todayCandles had fewer than 20 bars until ~09:35, which made Classify short-circuit to
        // Sideways/LOW and MarketRegimeGate hard-block every signal through the opening move.
        // indicatorCandles is the same rolling series EMA20/EMA50 already use. dayOpen is passed
        // separately so gap detection still measures today's open, not the oldest rolling bar.
        var marketRegime = ctx.Regime.Classify(
    ctx.Symbol,
    indicatorCandles,
    ema20,
    ema50,
    yesterdayClose,
    ctx.Instruments.IsExpiryDay(ctx.Symbol),
    dayOpen);

        // ── Events ────────────────────────────────────────────────────────────
        var upcomingEvents = await ctx.EventRepo.FindAsync(
            e => e.EventTimeUtc >= IstClock.Now.AddHours(-2) &&
                 e.EventTimeUtc <= IstClock.Now.AddHours(1));

        var newsEvent = ctx.EventCalendar.GetFireableNewsEvent(upcomingEvents);
        var strategyHint = TradingConst.StrategyType.Auto;

        // ── Audit snapshot (Market Snapshot Audit feature) ─────────────────────
        // Debugging/analytics only — built from indicators already computed above,
        // no extra queries. Not saved here; SignalWorker finishes and persists it once.
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
            Ema9 = ema9,
            Ema21 = ema21,
            Ema20_5m = ema20_5m,
            Ema50_5m = ema50_5m,
            Rsi = rsi,
            Rsi5m = rsi5m,
            AtrRaw = atrRaw,
            AtrPct = atrPct,
            Pcr = pcrValue,
            PcrFullChain = pcrFullChainValue,
            Vwap = vwap,
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
            Rsi5m: rsi5m,
            AtrPct: atrPct,
            AtrRaw: atrRaw,
            Ltp: ltp,
            YesterdayClose: yesterdayClose,
            CurrentVolume: currentVolume,
            AvgVolume20: avgVolume20,
            PriceChangePct: priceChangePct,
            DayOpen: sessionOpen,
            DayLow: sessionLow,
            DayHigh: sessionHigh,
            DayChangePct: dayChangePct,
            Vwap: vwap,
            PcrValue: pcrValue,
            StrategyHint: strategyHint,
            NewsEvent: newsEvent,
            LastTickAge: lastTickAge,
            LastOptionChainAge: lastOptionChainAge,
            LastClosed5mCandle: todayCandles5m.Count > 0 ? todayCandles5m[^1] : null)
        {
            Audit = audit,
            LastClosed1mCandle = indicatorCandles.Count > 0 ? indicatorCandles[^1] : null,
            Ema9 = ema9,
            Ema21 = ema21,
            Atr5m = Atr5mFor(indicatorCandles5m, ctx.Atr, DateTime.UtcNow),
        };
    }

    /// <summary>
    /// The 5m feed counts as stalled when the newest closed 5m bar CLOSED more than this long ago (two bar periods). Measured from the bar's close
    /// (CandleTime + 5 min), which is 15 minutes after the bar's start time; 5m ATR is then reported as missing.
    /// </summary>
    internal static readonly TimeSpan Atr5mMaxCloseAge = TimeSpan.FromMinutes(10);

    /// <summary>
    /// 5m ATR(14) from the closed 5m bars (oldest first), the same Wilder calculation and 100-bar window the backtest uses
    /// (IndicatorMath.Atr over Window5m). 0 when there are fewer than 15 bars or the newest bar closed more than
    /// <see cref="Atr5mMaxCloseAge"/> ago.
    /// </summary>
    internal static decimal Atr5mFor(IReadOnlyList<Candle> closed5m, AtrService atr, DateTime utcNow)
    {
        if (closed5m.Count < 15)
            return 0m;
        var newestClosedAt = closed5m[^1].CandleTime.AddMinutes(5);
        if (utcNow - newestClosedAt > Atr5mMaxCloseAge)
            return 0m;
        return atr.Calculate(closed5m as List<Candle> ?? closed5m.ToList(), 14);
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
