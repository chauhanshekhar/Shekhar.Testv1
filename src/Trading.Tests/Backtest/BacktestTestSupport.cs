using Trading.Backtest.Data;
using Trading.Backtest.Settings;
using Trading.Backtest.Simulation;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Tests.Backtest;

/// <summary>Shared builders for the backtest tests: a settings snapshot shaped like the live rows that matter, and synthetic sessions.</summary>
internal static class BtSupport
{
    public static readonly DateTime Day = new(2026, 10, 12);                  // a Monday, UTC calendar date
    public static readonly DateTime SessionOpenUtc = Day.AddHours(3).AddMinutes(45);   // 09:15 IST

    /// <summary>
    /// Settings rows that drive the CALL/PUT chain as the live database does (gates and exits are added per test). Every Enable* flag
    /// the chain reads defaults to ON when its row is missing, so the rows that switch a filter off must be present.
    /// </summary>
    public static BacktestSettings LiveLike()
    {
        var s = new BacktestSettings();
        s.Set("EnableTrendFilter", null, 1)
         .Set("EnableEmaGapFilter", null, 1).Set("EmaGapMinPoints", null, 10).Set("EmaGapMinPoints", "BANKNIFTY", 27.5m)
         .Set("CallRsiFloor", null, 45).Set("CallRsiCeiling", null, 65)
         .Set("PutRsiFloor", null, 28).Set("PutRsiCeiling", null, 38)
         .Set("EnableCallPcrFilter", null, 1).Set("CallPcrMinimum", null, 0.8m)
         .Set("EnablePutPcrFilter", null, 1).Set("PutPcrMinimum", "SENSEX", 0.7m)
         .Set("EnableCandleFilter", null, 0)
         .Set("EnableCallPrev5mHighFilter", null, 0, active: false).Set("EnablePutPrev5mLowFilter", null, 0, active: false)
         .Set("MinAtrPercent", "NIFTY", 0.03m).Set("MinAtrPercent", "BANKNIFTY", 0.03m).Set("MinAtrPercent", "SENSEX", 0.03m);
        return s;
    }

    public static TradingSettings Trading() => new();

    public static IReadOnlyList<TradingInstrument> Instruments() =>
    [
        new() { Symbol = "NIFTY", LotSize = 65, StrikeInterval = 50 },
        new() { Symbol = "BANKNIFTY", LotSize = 30, StrikeInterval = 100 },
        new() { Symbol = "SENSEX", LotSize = 20, StrikeInterval = 100 },
    ];

    public static MarketPoint CallPoint(string symbol, DateTime utc, double gap = 30, double rsi5 = 55, double? pcr = 1.0,
        double atrPct = 0.08, double atr = 20, double ltp = 24000)
    {
        var e50 = ltp - 100;
        var e20 = e50 + gap;
        return Build(symbol, utc, ltp: e20 + 10, e20, e50, rsi5, pcr, atrPct, atr);
    }

    public static MarketPoint PutPoint(string symbol, DateTime utc, double gap = 30, double rsi5 = 33, double? pcr = 1.0,
        double atrPct = 0.08, double atr = 20, double ltp = 24000)
    {
        var e50 = ltp + 100;
        var e20 = e50 - gap;
        return Build(symbol, utc, ltp: e20 - 10, e20, e50, rsi5, pcr, atrPct, atr);
    }

    private static MarketPoint Build(string symbol, DateTime utc, double ltp, double e20, double e50, double rsi5, double? pcr, double atrPct, double atr) =>
        new()
        {
            Symbol = symbol, TimeUtc = utc, NextBarIndex = (int)(utc - SessionOpenUtc).TotalMinutes,
            Ltp = ltp, Ema20 = e20, Ema50 = e50, Rsi1m = rsi5, Rsi5m = rsi5, Atr = atr, AtrPct = atrPct,
            DayChangePct = 0, DayOpen = ltp, DayLow = ltp - 50, DayHigh = ltp + 50,
            Ema20_5m = 0, Ema50_5m = 0, LastClosedBar = new Bar(utc.AddMinutes(-1), ltp, ltp, ltp, ltp), Pcr = pcr,
        };

    /// <summary>One session of 1m bars from 09:15 to 15:29 IST; closePrice(minuteIndex) gives each bar's close (open = previous close).</summary>
    public static Bar[] Bars(Func<int, double> closePrice)
    {
        var bars = new List<Bar>();
        var prev = closePrice(0);
        for (var i = 0; i < 375; i++)
        {
            var c = closePrice(i);
            bars.Add(new Bar(SessionOpenUtc.AddMinutes(i), prev, Math.Max(prev, c), Math.Min(prev, c), c));
            prev = c;
        }
        return bars.ToArray();
    }

    public static Bar[] FlatBars(double price = 24000) => Bars(_ => price);

    public static SymbolData Data(string symbol, Bar[] bars, params MarketPoint[] points) =>
        new() { Symbol = symbol, Points = points.OrderBy(p => p.TimeUtc).ToArray(), Bars = bars, StrikeInterval = 50 };

    public static ExitModel Exit(BacktestSettings s, int lotsOverride = 0)
    {
        var t = Trading();
        if (lotsOverride > 0) t.AdaptiveLotMin = lotsOverride;
        return ExitModel.From(s, t,
            new Dictionary<string, double> { ["NIFTY"] = 0.5, ["BANKNIFTY"] = 0.5, ["SENSEX"] = 0.5 },
            new Dictionary<string, double> { ["NIFTY"] = 100, ["BANKNIFTY"] = 100, ["SENSEX"] = 100 },
            new Dictionary<string, int> { ["NIFTY"] = 65, ["BANKNIFTY"] = 30, ["SENSEX"] = 20 });
    }

    /// <summary>Minute-of-session helper: 04:30 UTC is 10:00 IST.</summary>
    public static DateTime At(int hourUtc, int minuteUtc) => Day.AddHours(hourUtc).AddMinutes(minuteUtc);
}
