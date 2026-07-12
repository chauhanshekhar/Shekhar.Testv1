using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

/// <summary>
/// Calculates option SL/T1/T2 from the OPTION'S OWN price action (ATR + recent
/// support/resistance) instead of scaling the index's move by delta — a big index move
/// doesn't mean the option itself has room to run past its own recent range.
///
/// Groww's historical-candle API is only queried for the underlying index (see
/// HistoricalDataWorker) — no per-option OHLC candle feed exists. BuildCandlesFromSnapshots
/// below buckets the OptionChainData snapshots OptionChainWorker already persists every
/// ~30s into synthetic 1-minute candles, so no new ingestion worker is needed. The
/// still-forming bucket is always excluded, matching the same "wait for the bar to close"
/// rule already applied to index candles in MarketSnapshotBuilder.
/// </summary>
public class OptionTargetService
{
    private readonly AtrService _atr;

    public OptionTargetService(AtrService atr)
    {
        _atr = atr;
    }

    private const int AtrPeriod = 14;
    private const int SupportResistanceLookback = 20;
    private const int MinCandlesRequired = AtrPeriod + 1;

    /// <summary>
    /// Returns null when there isn't enough option-candle history yet (e.g. shortly after
    /// market open, or a strike just selected for the first time today) — caller should
    /// fall back to the delta-adjusted index-based estimate in that case.
    ///
    /// No call/put branch: this strategy only ever BUYS the option premium (CE when
    /// bullish, PE when bearish) — it never shorts premium — so the SL/target direction is
    /// identical for both: SL below entry, targets above, regardless of which side the
    /// underlying is expected to move.
    /// </summary>
    public OptionLevels? Calculate(
    List<Candle> optionCandles,
    decimal entry)
{
    if (optionCandles.Count < MinCandlesRequired || entry <= 0)
        return null;

    var atr = _atr.Calculate(optionCandles, AtrPeriod);

    if (atr <= 0)
        return null;


    // ==========================
    // SL
    // ==========================

    var risk = atr * 1.2m;


    var minRisk = entry * 0.06m; // 6%
    var maxRisk = entry * 0.15m; // 15%

    risk = Math.Max(risk, minRisk);
    risk = Math.Min(risk, maxRisk);


    var sl = entry - risk;


    // ==========================
    // Targets
    // ==========================


    var recent = optionCandles
        .TakeLast(20)
        .ToList();


    var range =
        recent.Max(x=>x.High)
        -
        recent.Min(x=>x.Low);


    var rangePct =
        range / entry * 100m;


    decimal t1Move;
    decimal t2Move;


    if(rangePct < 20)
    {
        // flat market
        t1Move = entry * 0.12m;
        t2Move = entry * 0.20m;
    }
    else
    {
        // trending market
        t1Move = entry * 0.20m;
        t2Move = entry * 0.35m;
    }


    var t1 = entry + t1Move;
    var t2 = entry + t2Move;


    return new OptionLevels(
        Math.Round(sl,2),
        Math.Round(t1,2),
        Math.Round(t2,2));
}
    /// <summary>
    /// Buckets OptionChainData snapshots for one trading symbol into synthetic 1-minute OHLC
    /// candles: Open = first Ltp seen in the minute, High/Low = max/min Ltp, Close = last Ltp,
    /// Volume = last cumulative Volume reported by Groww for that minute. The still-forming
    /// current-minute bucket is excluded.
    /// </summary>
    public static List<Candle> BuildCandlesFromSnapshots(List<OptionChainData> snapshots, string tradingSymbol)
    {
        var now = IstClock.Now;

        return snapshots
            .Where(s => s.TradingSymbol == tradingSymbol && s.Ltp > 0)
            .GroupBy(s => new DateTime(
                s.CreatedOn.Year, s.CreatedOn.Month, s.CreatedOn.Day,
                s.CreatedOn.Hour, s.CreatedOn.Minute, 0))
            .Where(g => g.Key.AddMinutes(1) <= now)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var ordered = g.OrderBy(x => x.CreatedOn).ToList();
                return new Candle
                {
                    Symbol = tradingSymbol,
                    CandleTime = g.Key,
                    Open = ordered[0].Ltp,
                    High = ordered.Max(x => x.Ltp),
                    Low = ordered.Min(x => x.Ltp),
                    Close = ordered[^1].Ltp,
                    Volume = ordered[^1].Volume,
                };
            })
            .ToList();
    }
}
