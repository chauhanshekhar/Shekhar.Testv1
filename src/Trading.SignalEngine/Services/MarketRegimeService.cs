using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

public enum MarketTrend
{
    Uptrend,
    Downtrend,
    Sideways
}

public enum MarketVolatility
{
    High,
    Low,
    Extreme
}

public record MarketRegime(
    MarketTrend Trend,
    MarketVolatility Volatility,
    bool IsExpiryDay,
    bool IsGapUp,
    bool IsGapDown,
    decimal Atr);

public class MarketRegimeService
{
    private static readonly Dictionary<string, decimal> AtrThreshold = new()
    {
        ["NIFTY"] = 5m,
        ["BANKNIFTY"] = 15m,
        ["FINNIFTY"] = 4m,
        ["SENSEX"] = 15m,
        ["MIDCAPNIFTY"] = 6m
    };

    // Upper cap — spike/panic markets where signals are unreliable
    private static readonly Dictionary<string, decimal> AtrMaxThreshold = new()
    {
        ["NIFTY"] = 20m,
        ["BANKNIFTY"] = 60m,
        ["FINNIFTY"] = 15m,
        ["SENSEX"] = 60m,
        ["MIDCAPNIFTY"] = 20m
    };

    private const decimal GapPercent = 0.30m;

    /// <summary>Per-symbol extreme-volatility ceiling, reused by MarketClassificationService.</summary>
    public static decimal GetAtrMaxThreshold(string symbol) => AtrMaxThreshold.GetValueOrDefault(symbol, 20m);

    /// <summary>Per-symbol "High" volatility floor.</summary>
    public static decimal GetAtrThreshold(string symbol) => AtrThreshold.GetValueOrDefault(symbol, 5m);

    private readonly AtrService _atr;

    public MarketRegimeService(
        AtrService atr)
    {
        _atr = atr;
    }

    /// <param name="candles">
    /// ROLLING indicator candles (history included), not today-only. Previously the caller passed
    /// today's candles alone, so before the 20th bar of the session -- roughly 09:35 -- this method
    /// short-circuited and asserted Sideways/LOW. MarketRegimeGate treats Low volatility as a hard
    /// block, so every signal was suppressed for the first ~20 minutes of the signal window no
    /// matter what the market was doing, including on a large opening gap. Rolling history is the
    /// same source EMA20/EMA50 already use, and AtrService applies Wilder smoothing forward to the
    /// most recent bar, so ATR stays seeded from yesterday and converges as today's bars arrive.
    /// </param>
    /// <param name="dayOpen">
    /// Today's opening price, used only for gap detection. Passed explicitly because `candles` is
    /// now rolling, so its First() is no longer today's first bar.
    /// </param>
    public MarketRegime Classify(
        string symbol,
        List<Candle> candles,
        decimal ema20,
        decimal ema50,
        decimal yesterdayClose = 0m,
        bool isExpiryDay = false,
        decimal dayOpen = 0m)
    {
        // Gap direction is resolved FIRST: it needs only today's open and yesterday's close, never
        // 20 bars. Computing it after the warmup guard (as before) meant the flags read false/false
        // during exactly the window a gap matters.
        bool isGapUp = false;
        bool isGapDown = false;

        if (yesterdayClose > 0 && dayOpen > 0)
        {
            var openGapPct = (dayOpen - yesterdayClose) / yesterdayClose * 100m;
            isGapUp = openGapPct > GapPercent;
            isGapDown = openGapPct < -GapPercent;
        }

        // Genuine no-data safety net only. With rolling candles this is now reached solely when
        // history really is absent (first run, newly added symbol) -- not every morning. Gap flags
        // are carried through so callers still see the opening context.
        if (candles == null ||
            candles.Count < 20)
        {
            return new MarketRegime(
                MarketTrend.Sideways,
                MarketVolatility.Low,
                isExpiryDay,
                isGapUp,
                isGapDown,
                0m);
        }

        var orderedCandles =
            candles
            .OrderBy(c => c.CandleTime)
            .ToList();

        var atrValue =
            _atr.Calculate(
                orderedCandles,
                14);


        var threshold    = AtrThreshold.GetValueOrDefault(symbol, 5m);

        var maxThreshold = AtrMaxThreshold.GetValueOrDefault(symbol, 20m);

        var volatility = atrValue >= maxThreshold
            ? MarketVolatility.Extreme
            : atrValue >= threshold
                ? MarketVolatility.High
                : MarketVolatility.Low;

        var lastClose =
            orderedCandles.Last().Close;

        MarketTrend trend;


        const decimal BufferPercent = 0.0002m; // 0.02%

        var emaBuffer = ema50 * BufferPercent;

        if (ema20 > ema50 + emaBuffer &&
            lastClose > ema20)
        {
            trend = MarketTrend.Uptrend;
        }
        else if (ema20 < ema50 - emaBuffer &&
                 lastClose < ema20)
        {
            trend = MarketTrend.Downtrend;
        }
        else
        {
            trend = MarketTrend.Sideways;
        }
        return new MarketRegime(
            trend,
            volatility,
            isExpiryDay,
            isGapUp,
            isGapDown,
            atrValue);
    }

    /// <summary>
    /// Final trade filter.
    /// Reject:
    /// - Low volatility
    /// - Extreme volatility
    /// - Expiry day before 09:30
    ///
    /// Sideways is deliberately NOT rejected here. This method is reached only from
    /// MarketRegimeGate, a pre-gate that runs before StrategyManager picks a strategy, so any
    /// rule added here is unavoidably global and would also suppress HeroZero and Expiry, which
    /// carry their own independent entry rules. The Sideways restriction is therefore enforced
    /// one layer later, in StrategySelectorService, where the selected strategy is known and it
    /// can be applied to Directional alone.
    /// </summary>
    public static bool IsTradeAllowed(
        MarketRegime regime)
    {

        if (regime.Volatility == MarketVolatility.Low ||
            regime.Volatility == MarketVolatility.Extreme)
        {
            return false;
        }

        if (regime.IsExpiryDay &&
        IstClock.Now.TimeOfDay < new TimeSpan(9, 30, 0))
        {
            return false;
        }

        return true;
    }
}