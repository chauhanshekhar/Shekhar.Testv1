using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services.Models;

/// <summary>
/// Market inputs both direction evaluators read. Settings are NOT carried here - each
/// evaluator resolves its own from IStrategySettingsService (an in-memory cache), so a
/// direction's specification lives in exactly one place.
/// </summary>
public readonly record struct EntryMarketInputs(
    decimal Ltp,
    decimal Ema20,
    decimal Ema50,
    decimal Rsi,
    decimal Pcr,
    decimal AtrPct,
    decimal PriceChangePct,
    decimal CurrentVolume,
    decimal AvgVolume20,
    decimal Vwap,
    decimal Prev5mHigh,
    decimal Prev5mLow,
    decimal DayOpen,
    decimal DayLow,
    decimal DayHigh,
    decimal DayChangePct,
    decimal CeAtmDelta,
    decimal PeAtmDelta,
    long CeChangeInOi,
    long PeChangeInOi,
    bool TrendBullish,
    bool TrendBearish,
    Candle? LastClosedCandle);
