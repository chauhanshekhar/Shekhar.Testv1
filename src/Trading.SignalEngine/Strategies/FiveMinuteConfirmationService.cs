using Trading.Infrastructure;

namespace Trading.SignalEngine.Strategies;

/// <summary>Outcome of the 5-minute market-context check for one direction.</summary>
/// <param name="Passed">True when the 5m environment supports this direction.</param>
/// <param name="Reason">Why it failed. Null on a pass.</param>
/// <param name="TrendDirection5m">Bullish / Bearish / Sideways, derived from the 5m EMA structure.</param>
public readonly record struct FiveMinuteConfirmation(
    bool Passed,
    string? Reason,
    string TrendDirection5m)
{
    public static FiveMinuteConfirmation Fail(string reason, string trend) => new(false, reason, trend);
    public static FiveMinuteConfirmation Pass(string trend) => new(true, null, trend);
}

/// <summary>
/// 5-MINUTE MARKET CONFIRMATION — the environment check that must hold before the existing
/// 1-minute entry logic is allowed to fire.
///
///     5m  -> does the market environment support a CALL / a PUT at all?
///     1m  -> is this the moment to enter?   (unchanged, still the final trigger)
///
/// Off by default. Gated per-symbol by the StrategySettings flag "UseFiveMinuteConfirmation";
/// when it is off this type is never consulted and behaviour is byte-identical to before.
///
/// ── NO NEW NUMBERS ───────────────────────────────────────────────────────────────────────────
/// Every threshold here is an EXISTING configured setting reused as-is:
///   * the EMA structure test is the same expression MarketSnapshotBuilder already uses to derive
///     TrendDirection, applied to the 5m EMAs instead of the 1m ones;
///   * the RSI test reuses RsiBullConfirm / RsiBearConfirm, the settings
///     MarketClassificationService already uses to confirm direction for Breakout/Breakdown.
/// Nothing was invented, and no existing threshold value was changed.
///
/// ── WHAT IS DELIBERATELY ABSENT ──────────────────────────────────────────────────────────────
/// A 5-minute MarketState and a 5-minute MarketRegime do not exist in this system, and are NOT
/// fabricated here. MarketClassificationService is fed 1m EMAs, 1m RSI, 1m ATR and 1m price
/// change; MarketRegimeService runs on 1m rolling candles and its AtrThreshold/AtrMaxThreshold
/// constants are calibrated for 1-minute bars — a 5-minute ATR is several times larger, so
/// reusing those numbers against it would be meaningless. Producing either would require
/// inventing thresholds, which this task explicitly forbids. See the task report for the gap list.
/// </summary>
public class FiveMinuteConfirmationService
{
    public const string EnabledSettingKey = "UseFiveMinuteConfirmation";

    /// <summary>Derives the 5m trend from the EMA structure — the same expression
    /// MarketSnapshotBuilder uses for the 1m TrendDirection, applied to the 5m series.</summary>
    public static string TrendDirection(decimal ema20_5m, decimal ema50_5m) =>
        ema20_5m > ema50_5m ? "Bullish"
        : ema20_5m < ema50_5m ? "Bearish"
        : "Sideways";

    /// <summary>
    /// Confirms the 5-minute environment for the given direction.
    ///
    /// All inputs come from CLOSED 5-minute candles: MarketSnapshotBuilder excludes the forming
    /// bar from indicatorCandles5m before computing Ema20_5m / Ema50_5m / Rsi5m, so a partially
    /// formed 5m candle can never influence this decision.
    ///
    /// Fails CLOSED when an input is unavailable — "cannot confirm" is not "confirmed".
    /// </summary>
    public FiveMinuteConfirmation Confirm(
        bool call,
        decimal ema20_5m,
        decimal ema50_5m,
        decimal? rsi5m,
        TradingSettings settings)
    {
        var trend = TrendDirection(ema20_5m, ema50_5m);

        // EMA20_5m/EMA50_5m are 0 until 20 closed 5m bars exist (~100 minutes into the session).
        // Zero is "not computed yet", not a real reading.
        if (ema20_5m <= 0 || ema50_5m <= 0)
            return FiveMinuteConfirmation.Fail("5m EMAs not available yet", trend);

        // Rsi5m is null until 15 closed 5m bars exist (~75 minutes in).
        if (rsi5m == null)
            return FiveMinuteConfirmation.Fail("5m RSI not available yet", trend);

        if (call)
        {
            if (trend != "Bullish")
                return FiveMinuteConfirmation.Fail(
                    $"5m trend is {trend}, EMA20_5m {ema20_5m:F2} not above EMA50_5m {ema50_5m:F2}", trend);

            if (rsi5m.Value < settings.RsiBullConfirm)
                return FiveMinuteConfirmation.Fail(
                    $"5m RSI {rsi5m.Value:F2} below RsiBullConfirm {settings.RsiBullConfirm:F0}", trend);

            return FiveMinuteConfirmation.Pass(trend);
        }

        if (trend != "Bearish")
            return FiveMinuteConfirmation.Fail(
                $"5m trend is {trend}, EMA20_5m {ema20_5m:F2} not below EMA50_5m {ema50_5m:F2}", trend);

        if (rsi5m.Value > settings.RsiBearConfirm)
            return FiveMinuteConfirmation.Fail(
                $"5m RSI {rsi5m.Value:F2} above RsiBearConfirm {settings.RsiBearConfirm:F0}", trend);

        return FiveMinuteConfirmation.Pass(trend);
    }
}
