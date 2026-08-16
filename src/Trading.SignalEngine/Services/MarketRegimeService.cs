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

    /// <summary>Per-symbol "High" volatility floor, reused by ExpiryMarketScoreService's ATR band scoring.</summary>
    public static decimal GetAtrThreshold(string symbol) => AtrThreshold.GetValueOrDefault(symbol, 5m);

    private readonly AtrService _atr;

    public MarketRegimeService(
        AtrService atr)
    {
        _atr = atr;
    }

    public MarketRegime Classify(
        string symbol,
        List<Candle> candles,
        decimal ema20,
        decimal ema50,
        decimal yesterdayClose = 0m,
        bool isExpiryDay = false)
    {
        if (candles == null ||
            candles.Count < 20)
        {
            return new MarketRegime(
                MarketTrend.Sideways,
                MarketVolatility.Low,
                isExpiryDay,
                false,
                false,
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
        bool isGapUp = false;
        bool isGapDown = false;

        if (yesterdayClose > 0)
        {
            var todayOpen =
                orderedCandles
                .First()
                .Open;

            var gapPct =
                ((todayOpen - yesterdayClose)
                / yesterdayClose)
                * 100m;

            isGapUp =
                gapPct > GapPercent;

            isGapDown =
                gapPct < -GapPercent;
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
    /// - Sideways trend
    /// - Expiry day
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