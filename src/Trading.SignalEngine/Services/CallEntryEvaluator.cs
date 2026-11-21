using Trading.Application.Interfaces;
using Trading.SignalEngine.Services.Models;

namespace Trading.SignalEngine.Services;

/// <summary>
/// CALL entry specification - up to 11 conditions, each appended to the list only when its
/// Enable* flag (if any) is on AND every threshold it reads is IsSettingActive. A condition that
/// does not apply is simply absent from DirectionVerdict.Checks - not present-and-true - so a
/// checklist read off a live signal only ever shows conditions that were actually evaluated.
///
///   1 EMA20 &gt; EMA50             7 Body% >= CallBodyPctMinimum
///   2 LTP &gt; EMA20               8 Volume &gt; VolumeSMA20  (NOT WIRED - no volume data)
///   3 LTP &gt; VWAP  (NOT WIRED - no index volume, so no VWAP)
///   4 LTP &gt; Prev 5m High        9 ATR% >= CallMinAtrPercent
///   5 RSI CallRsiFloor..Ceiling  10 CE Delta in [CallDeltaMinimum(0.35), CallDeltaMaximum(0.65)]
///   6 Bullish candle            11 DayChange% > min OR (Recovery% > min AND LTP &gt; DayOpen)
///                               12 PCR >= CallPcrMinimum (off by default - see condition 12)
///
/// CONDITIONS 3 AND 8 ARE NOT WIRED BECAUSE THEIR INPUTS DO NOT EXIST. Groww publishes no index
/// candle volume, so Vwap is NULL on every row (verified 2026-09-02: 328,092 of 328,092) and
/// every Candle.Volume is 0. Wiring them live would suppress every CALL forever - they need a
/// volume-bearing feed to mean anything.
///
/// TWO PREVIOUS CONDITIONS ARE RETIRED behind flags rather than deleted: the 5m EMA trend
/// alignment, and PriceChangePct measured against today's OPEN (superseded by 11, which measures
/// against YESTERDAY'S CLOSE and adds the recovery-from-low path).
///
/// Missing inputs FAIL CLOSED - a 0 prev5mHigh or dayLow yields no CALL, because "unknown" must
/// never read as "confirmed" on a live account.
/// </summary>
public class CallEntryEvaluator
{
    private readonly IStrategySettingsService _strategySettings;

    public CallEntryEvaluator(IStrategySettingsService strategySettings)
    {
        _strategySettings = strategySettings;
    }

    public DirectionVerdict Evaluate(string symbol, in EntryMarketInputs m)
    {
        var conditions = new List<(string Description, bool Passed)>();

        bool Active(string key) =>
            _strategySettings.IsSettingActive(key, symbol);

        bool Enabled(string key, bool defaultValue = true) =>
            _strategySettings.GetBool(key, symbol, defaultValue);

        decimal Threshold(string key, decimal defaultValue) =>
            _strategySettings.GetDecimal(key, symbol, defaultValue);

        // 1 + 2: Trend
        if (Enabled("EnableTrendFilter"))
        {
            conditions.Add((
                $"1 EMA20 > EMA50 ({m.Ema20:F2} vs {m.Ema50:F2})",
                m.Ema20 > m.Ema50));

            conditions.Add((
                $"2 LTP > EMA20 ({m.Ltp:F2} vs {m.Ema20:F2})",
                m.Ltp > m.Ema20));
        }

        // 2b: EMA gap (shared CALL/PUT condition, off by default - see EmaGapCondition).
        if (EmaGapCondition.Evaluate(m.Ema20, m.Ema50, Enabled, Active, Threshold) is { } emaGap)
            conditions.Add(emaGap);

        // 3 - LTP > VWAP. Not wired: Groww publishes no index candle volume, so Vwap is NULL on
        // every row (verified 2026-09-02: 328,092 of 328,092) and every Candle.Volume is 0.
        // Wiring this live would suppress every CALL forever - it needs a volume-bearing feed.

        // 4: Previous five-minute high
        if (Enabled("EnableCallPrev5mHighFilter"))
        {
            conditions.Add((
                $"4 LTP > Prev5mHigh ({(m.Prev5mHigh > 0 ? m.Prev5mHigh.ToString("F2") : "unavailable")})",
                m.Prev5mHigh > 0 && m.Ltp > m.Prev5mHigh));
        }

        // 5: RSI
        if (Active("CallRsiFloor")
            && Active("CallRsiCeiling"))
        {
            var floor = Threshold("CallRsiFloor", 60m);
            var ceiling = Threshold("CallRsiCeiling", 70m);

            var passed = m.Rsi >= floor && m.Rsi <= ceiling;
            var description = $"5 RSI in [{floor},{ceiling}]";

            conditions.Add((
                $"{description} - was {m.Rsi:F2}",
                passed));
        }

        // 6 + 7: Bullish candle
        if (Enabled("EnableCandleFilter") && Active("CallBodyPctMinimum"))
        {
            var minimum = Threshold("CallBodyPctMinimum", 30m);
            var (candleBullish, _) = CandleConfirmation.Confirm(m.LastClosedCandle, minimum);

            conditions.Add((
                $"6+7 Bullish candle, body >= {minimum}%",
                candleBullish));
        }

        // 8 - Volume > VolumeSMA20. Not wired for the same reason as condition 3: Groww publishes
        // no index candle volume, so every Candle.Volume is 0 and CurrentVolume/AvgVolume20 are
        // always 0 (verified 2026-09-02: MAX(Volume)=0 on every series, every symbol) - there is
        // no data for this condition to read.

        // 9: ATR
        if (Active("CallMinAtrPercent"))
        {
            var minimum = Threshold("CallMinAtrPercent", 0.03m);

            conditions.Add((
                $"9 ATR% >= {minimum:F4} - was {m.AtrPct:F4}",
                m.AtrPct >= minimum));
        }

        // 10: Delta - controlled only by threshold activation, no separate enable flag
        if (Active("CallDeltaMinimum") && Active("CallDeltaMaximum"))
        {
            var minimum = Threshold("CallDeltaMinimum", 0.35m);
            var maximum = Threshold("CallDeltaMaximum", 0.65m);

            conditions.Add((
                $"10 CE Delta in [{minimum},{maximum}] - was {m.CeAtmDelta:F3}",
                m.CeAtmDelta >= minimum && m.CeAtmDelta <= maximum));
        }

        // 11: Day change or recovery
        if (Active("CallDayChangePctMin")
            && Active("CallRecoveryPctMin"))
        {
            var dayMinimum = Threshold("CallDayChangePctMin", 0.10m);
            var recoveryMinimum = Threshold("CallRecoveryPctMin", 0.40m);
            var recoveryPct = m.DayLow > 0
                ? (m.Ltp - m.DayLow) / m.DayLow * 100m
                : 0m;

            conditions.Add((
                $"11 DayChange% > {dayMinimum} (was {m.DayChangePct:F3}) " +
                $"OR Recovery% > {recoveryMinimum} (was {recoveryPct:F3}) with LTP > DayOpen",
                m.DayChangePct > dayMinimum
                    || (recoveryPct > recoveryMinimum
                        && m.DayOpen > 0
                        && m.Ltp > m.DayOpen)));
        }
        // CALL: Allow entry only after a fresh session-high breakout.
        if (Enabled("EnableCallFreshDayHighFilter"))
        {
            conditions.Add((
                "CALL: Fresh session high breakout required",
                m.DayHigh > 0 &&
                m.Ltp > m.DayHigh + 5m
            ));
        }


        // 12: PCR - off by default (EnableCallPcrFilter). Historically counter-indicated on the
        // closed trades analysed (CALL winners averaged PCR 0.90, losers 1.17), so this must be
        // turned on deliberately per symbol after a fresh backtest.
        if (Enabled("EnableCallPcrFilter", false) && Active("CallPcrMinimum"))
        {
            var minimum = Threshold("CallPcrMinimum", 1.05m);

            conditions.Add((
                $"12 PCR >= {minimum} - was {m.Pcr:F3}",
                m.Pcr >= minimum));
        }

        // 12b: PCR ceiling - CALL only, applies once a CallPcrMaximum row exists and is active (no Enable flag; an
        // unconfigured key is off). 1.5 is the compiled default if a row carries no usable value.
        if (Active("CallPcrMaximum"))
        {
            var maximum = Threshold("CallPcrMaximum", 1.5m);

            conditions.Add((
                $"12b PCR <= {maximum} - was {m.Pcr:F3}",
                m.Pcr <= maximum));
        }

        // 13: Open-interest change
        if (Active("CallOiChangeMinimum"))
        {
            var minimum = Threshold("CallOiChangeMinimum", -1000m);

            conditions.Add((
                $"13 CE ChangeInOi >= {minimum} - was {m.CeChangeInOi}",
                m.CeChangeInOi >= minimum));
        }

        if (Active("PriceChangeThreshold"))
        {
            var threshold = Threshold("PriceChangeThreshold", 0.10m);

            conditions.Add((
                $"[retired] PriceChangePct > {threshold}",
                m.PriceChangePct > threshold));
        }

        return new DirectionVerdict(conditions.ToArray());
    }
}
