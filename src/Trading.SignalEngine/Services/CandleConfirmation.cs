using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

/// <summary>
/// THE entry-candle confirmation mechanism, shared by the AND-chain and by the approved-rule
/// path so both ask the identical question of the identical candle.
///
/// The candle passed in is always a CLOSED bar — MarketSnapshotBuilder excludes the forming
/// one from both series before LastClosed1mCandle / LastClosed5mCandle are set.
///
/// Confirmation is direction PLUS body strength: a doji or a long-wicked bar that closed the
/// right way is not a confirmation. bodyPctMinimum is the existing per-symbol setting.
/// </summary>
public static class CandleConfirmation
{
    public static (bool Bullish, bool Bearish) Confirm(Candle? candle, decimal bodyPctMinimum)
    {
        if (candle == null) return (false, false);

        var range = candle.High - candle.Low;
        if (range <= 0) return (false, false);

        var bodyPct = Math.Abs(candle.Close - candle.Open) / range * 100m;
        if (bodyPct < bodyPctMinimum) return (false, false);

        return (candle.Close > candle.Open, candle.Close < candle.Open);
    }
}
