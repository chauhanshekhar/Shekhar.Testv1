using Trading.Application.Interfaces;
using Trading.SignalEngine.Services.Models;

namespace Trading.SignalEngine.Services;

/// <summary>
/// PUT entry specification - the exact mirror of <see cref="CallEntryEvaluator"/>, including the
/// list-based construction: a condition is appended only when its Enable* flag (if any) is on AND
/// every threshold it reads is IsSettingActive, so DirectionVerdict.Checks only ever shows
/// conditions that were actually evaluated.
///
///   1 EMA20 &lt; EMA50             7 Body% >= PutBodyPctMinimum
///   2 LTP &lt; EMA20               8 Volume &gt; VolumeSMA20  (NOT WIRED - no volume data)
///   3 LTP &lt; VWAP  (NOT WIRED - no index volume, so no VWAP)
///   4 LTP &lt; Prev 5m Low         9 ATR% >= PutMinAtrPercent
///   5 RSI PutRsiFloor..Ceiling   10 |PE Delta| in [PutDeltaMinimum(0.35), PutDeltaMaximum(0.65)]
///   6 Bearish candle            11 DayChange% &lt; max OR (Fall% > min AND LTP &lt; DayOpen)
///                               12 PCR &lt;= PutPcrMaximum (off by default - see condition 12)
///
/// Fall% = (DayHigh - LTP) / DayHigh * 100.
///
/// DELTA SIGN is the one place the mirror is not literal: a put's delta is NEGATIVE (-0.50 for an
/// ATM PE), so the band is applied to the ABSOLUTE value. Comparing the raw negative against a
/// positive band would reject every put ever quoted.
/// </summary>
public class PutEntryEvaluator
{
    private readonly IStrategySettingsService _strategySettings;

    public PutEntryEvaluator(IStrategySettingsService strategySettings)
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
                $"1 EMA20 < EMA50 ({m.Ema20:F2} vs {m.Ema50:F2})",
                m.Ema20 < m.Ema50));

            conditions.Add((
                $"2 LTP < EMA20 ({m.Ltp:F2} vs {m.Ema20:F2})",
                m.Ltp < m.Ema20));
        }

        // 3 - LTP < VWAP. Not wired for the same reason as the CALL side: no index volume means
        // no VWAP to compare against.

        // 4: Previous five-minute low
        if (Enabled("EnablePutPrev5mLowFilter"))
        {
            conditions.Add((
                $"4 LTP < Prev5mLow ({(m.Prev5mLow > 0 ? m.Prev5mLow.ToString("F2") : "unavailable")})",
                m.Prev5mLow > 0 && m.Ltp < m.Prev5mLow));
        }

        // 5: RSI
        if (Enabled("EnableRsiFilter")
            && Active("PutRsiFloor")
            && Active("PutRsiCeiling"))
        {
            var floor = Threshold("PutRsiFloor", 28m);
            var ceiling = Threshold("PutRsiCeiling", 40m);

            var passed = m.Rsi >= floor && m.Rsi <= ceiling;
            var description = $"5 RSI in [{floor},{ceiling}]";

            if (Enabled("EnableRsiPullbackFilter", false)
                && Active("RsiPutBouncebackFloor")
                && Active("RsiPutBouncebackCeiling"))
            {
                var bouncebackFloor = Threshold("RsiPutBouncebackFloor", 60m);
                var bouncebackCeiling = Threshold("RsiPutBouncebackCeiling", 70m);

                passed |= m.Rsi >= bouncebackFloor && m.Rsi <= bouncebackCeiling;
                description += $" or [{bouncebackFloor},{bouncebackCeiling}]";
            }

            conditions.Add((
                $"{description} - was {m.Rsi:F2}",
                passed));
        }

        // 6 + 7: Bearish candle
        if (Enabled("EnableCandleFilter") && Active("PutBodyPctMinimum"))
        {
            var minimum = Threshold("PutBodyPctMinimum", 30m);
            var (_, candleBearish) = CandleConfirmation.Confirm(m.LastClosedCandle, minimum);

            conditions.Add((
                $"6+7 Bearish candle, body >= {minimum}%",
                candleBearish));
        }

        // 8 - Volume > VolumeSMA20. Not wired for the same reason as the CALL side: no index
        // candle volume exists, so the condition has nothing to read.

        // 9: ATR
        if (Enabled("EnablePutAtrFilter") && Active("PutMinAtrPercent"))
        {
            var minimum = Threshold("PutMinAtrPercent", 0.03m);

            conditions.Add((
                $"9 ATR% >= {minimum:F4} - was {m.AtrPct:F4}",
                m.AtrPct >= minimum));
        }

        // 10: |PE Delta| - controlled only by threshold activation, no separate enable flag.
        // DELTA SIGN: a put's delta is NEGATIVE (-0.50 for an ATM PE), so the band is applied to
        // the absolute value - comparing the raw negative against a positive band would reject
        // every put ever quoted.
        if (Active("PutDeltaMinimum") && Active("PutDeltaMaximum"))
        {
            var minimum = Threshold("PutDeltaMinimum", 0.35m);
            var maximum = Threshold("PutDeltaMaximum", 0.65m);

            conditions.Add((
                $"10 |PE Delta| in [{minimum},{maximum}] - was {m.PeAtmDelta:F3}",
                Math.Abs(m.PeAtmDelta) >= minimum && Math.Abs(m.PeAtmDelta) <= maximum));
        }

        // 11: Day change or fall. Fall% = (DayHigh - LTP) / DayHigh * 100.
        if (Enabled("EnablePutDayFilter")
            && Active("PutDayChangePctMax")
            && Active("PutFallPctMin"))
        {
            var dayMaximum = Threshold("PutDayChangePctMax", -0.10m);
            var fallMinimum = Threshold("PutFallPctMin", 0.40m);
            var fallPct = m.DayHigh > 0
                ? (m.DayHigh - m.Ltp) / m.DayHigh * 100m
                : 0m;

            conditions.Add((
                $"11 DayChange% < {dayMaximum} (was {m.DayChangePct:F3}) " +
                $"OR Fall% > {fallMinimum} (was {fallPct:F3}) with LTP < DayOpen",
                m.DayChangePct < dayMaximum
                    || (fallPct > fallMinimum
                        && m.DayOpen > 0
                        && m.Ltp < m.DayOpen)));
        }

        // 12: PCR - off by default (EnablePutPcrFilter). On the closed trades analysed, PUT
        // winners/losers overlapped almost completely on PCR, so this discriminated nothing;
        // enable per symbol only after a fresh backtest supports it.
        if (Enabled("EnablePutPcrFilter", false) && Active("PutPcrMaximum"))
        {
            var maximum = Threshold("PutPcrMaximum", 0.90m);

            conditions.Add((
                $"12 PCR <= {maximum} - was {m.Pcr:F3}",
                m.Pcr <= maximum));
        }

        // 13: Open-interest change. See the CALL side for why this defaults to the -1000
        // tolerance rather than a strict "> 0".
        if (Enabled("EnablePutOiFilter") && Active("PutOiChangeMinimum"))
        {
            var minimum = Threshold("PutOiChangeMinimum", -1000m);

            conditions.Add((
                $"13 PE ChangeInOi >= {minimum} - was {m.PeChangeInOi}",
                m.PeChangeInOi >= minimum));
        }

        if (Enabled("EnablePut5mTrendFilter", false))
        {
            conditions.Add((
                "[retired] 5m Trend Bearish",
                m.TrendBearish));
        }

        if (Enabled("EnablePutPriceChangeFilter", false)
            && Active("PriceChangeThreshold"))
        {
            var threshold = Threshold("PriceChangeThreshold", 0.10m);

            conditions.Add((
                $"[retired] PriceChangePct < -{threshold}",
                m.PriceChangePct < -threshold));
        }

        return new DirectionVerdict(conditions.ToArray());
    }
}
