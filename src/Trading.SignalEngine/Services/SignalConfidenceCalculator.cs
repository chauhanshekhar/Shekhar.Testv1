namespace Trading.SignalEngine.Services;

public static class SignalConfidenceCalculator
{
    public static decimal ComputeConfidence(
        bool buy,
        decimal ema20,
        decimal ema50,
        decimal ltp,
        decimal pcr,
        decimal rsi,
        decimal currentVolume,
        decimal avgVolume20,
        decimal bodyPct,
        decimal rsiCallFloor,
        decimal rsiCallCeiling,
        decimal rsiPutFloor,
        decimal rsiPutCeiling,
        decimal bodyPctMinimum)
    {
        decimal score = 25m; // base score

        // EMA strength
        var emaSepPct = ema50 > 0
            ? Math.Abs(ema20 - ema50) / ema50 * 100m
            : 0m;

        if (emaSepPct >= 0.20m)
        {
            score += 20m;
        }
        else if (emaSepPct >= 0.10m)
        {
            score += 15m;
        }
        else if (emaSepPct >= 0.05m)
        {
            score += 8m;
        }
        else
        {
            score += 0m;
        }

        // PCR
        if (buy)
            score += pcr > 1.20m ? 15m :
                     pcr > 1.05m ? 8m : 0m;
        else
            score += pcr < 0.80m ? 15m :
                     pcr < 0.95m ? 8m : 0m;

        // RSI
        if (buy)
        {
            if (rsi >= rsiCallCeiling)
                score += 15m;
            else if (rsi >= rsiCallFloor)
                score += 8m;
        }
        else
        {
            if (rsi <= rsiPutFloor)
                score += 15m;
            else if (rsi <= rsiPutCeiling)
                score += 8m;
        }

        // Volume scoring intentionally removed. currentVolume/avgVolume20 are still accepted as
        // parameters (callers and signatures unchanged) but contribute nothing to the score.
        // Groww publishes no index candle volume on this data path -- every Candle row is
        // Volume=0 -- so currentVolume was always 0 and the old `if (currentVolume > 0 ...)`
        // bonus could never fire. Deleting the branch is therefore behaviour-neutral; it just
        // stops the formula implying a volume input it never actually had.
        // Practical effect on the scale: max attainable score is 85, not 99
        // (25 base + 20 EMA + 15 PCR + 15 RSI + 10 candle). Relevant when reading the
        // hardcoded 70 confidence floor in StrategyManager.

        // Candle quality
        if (bodyPct >= bodyPctMinimum + 30m)
        {
            score += 10m;
        }
        else if (bodyPct >= bodyPctMinimum)
        {
            score += 5m;
        }

        return Math.Min(score, 99m);
    }
}
