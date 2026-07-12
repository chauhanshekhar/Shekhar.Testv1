namespace Trading.SignalEngine.Scoring.Services;

/// <summary>Max 20 — reuses the EMA values already computed for MarketSnapshot.</summary>
public class TrendScoreService
{
    public decimal Score(
        bool bullish,
        decimal ltp,
        decimal ema20,
        decimal ema50,
        decimal ema20_5m,
        decimal ema50_5m,
        decimal maxScore = 20m)
    {
        var oneMinuteConfirms = bullish
            ? ltp > ema20 && ema20 > ema50
            : ltp < ema20 && ema20 < ema50;

        var fiveMinuteConfirms = bullish
            ? ema20_5m > 0 && ema20_5m > ema50_5m
            : ema20_5m > 0 && ema20_5m < ema50_5m;

        if (oneMinuteConfirms && fiveMinuteConfirms)
            return maxScore;

        if (oneMinuteConfirms || fiveMinuteConfirms)
            return maxScore / 2m;

        return 0m;
    }
}
