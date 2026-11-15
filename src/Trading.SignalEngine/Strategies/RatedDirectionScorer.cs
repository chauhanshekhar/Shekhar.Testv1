using Trading.Application.Interfaces;

namespace Trading.SignalEngine.Strategies;

/// <summary>Everything <see cref="RatedDirectionScorer"/> reads. Every field exists both live (MarketSnapshot) and in the replay
/// (Trading.Backtest MarketPoint), so the shadow and the backtest score the same thing.</summary>
public readonly record struct RatedDirectionInput(
    decimal Ltp,
    decimal Ema20,
    decimal Ema50,
    decimal? Pcr,
    decimal AtrPct,
    decimal DayChangePct,
    bool LastBarBullish,
    bool LastBarBearish);

/// <summary>Both side scores and the winner (null = no trade).</summary>
public readonly record struct RatedDirectionDecision(decimal CallScore, decimal PutScore, string? Winner, string Reason);

/// <summary>
/// RATED DIRECTION (no RSI): score CALL and PUT 0-100 side by side and take the side that wins, like HeroZero ranks CE against
/// PE. SHADOW / BACKTEST ONLY - StrategyManager records the decision after the existing strategy has decided and never trades it.
///
///   Trend     30  1m LTP &gt; EMA20 &gt; EMA50 (PUT mirrored) = 30; EMA20 vs EMA50 only = 15
///   EMA gap   15  |EMA20-EMA50| &gt;= 2 x EmaGapMinPoints = 15; &gt; EmaGapMinPoints = 10 (only in the side's trend direction)
///   Momentum  20  day change in the side's direction &gt; 0.5% = 12, &gt; 0.2% = 6; last closed 1m bar in the direction +8
///   PCR       20  CALL inside [CallPcrMinimum, CallPcrMaximum]; PUT &lt;= PutPcrMaximum (and &gt;= PutPcrMinimum where active)
///   ATR       15  AtrPct &gt;= MinAtrPercent
/// Winner = the higher side when it scores &gt;= RatedMinScore (default 70) AND beats the other by &gt;= RatedMinMargin (default 10).
/// The weights are a starting point, not evidence - the backtest sweep (--rated) decides the thresholds.
/// OI and 5m candles are left out on purpose: the replay has no OI and no 5m candle list, so they could not be validated.
/// </summary>
public class RatedDirectionScorer
{
    public const string StrategyTag = "RatedDirShadow";
    public const string EnableKey = "EnableRatedDirection";
    public const string MinScoreKey = "RatedMinScore";
    public const string MinMarginKey = "RatedMinMargin";
    public const decimal DefaultMinScore = 70m;
    public const decimal DefaultMinMargin = 10m;

    private readonly IStrategySettingsService _settings;

    public RatedDirectionScorer(IStrategySettingsService settings) => _settings = settings;

    public bool IsEnabled(string symbol) => _settings.GetBool(EnableKey, symbol, false);

    // ── Rated CALL trading (PAPER ONLY) ───────────────────────────────────────────────────────
    // The replay supported CALL only (score >= 65, margin >= 30: +47,673 after costs, 10-07..10-28); PUT had no edge, so the
    // trading path takes CALL winners only. Off by default; any mode other than Paper never trades, enforced here.
    public const string RuleId = "RATED_CALL";
    public const string TradeTag = "RatedCall";
    public const string TradeEnableKey = "EnableRatedCallTrading";

    public bool IsTradingEnabled(string symbol) => _settings.GetBool(TradeEnableKey, symbol, false);

    /// <summary>The CALL decision to trade, or null with the reason. Uses RatedMinScore / RatedMinMargin.</summary>
    public RatedDirectionDecision? DecideCallTrade(string symbol, bool isPaper, in RatedDirectionInput m, out string reason)
    {
        if (!IsTradingEnabled(symbol)) { reason = "Rated CALL trading disabled"; return null; }
        if (!isPaper) { reason = "Rated CALL is paper-only"; return null; }

        var d = Decide(symbol, m);
        if (d.Winner != "CALL") { reason = d.Winner is null ? d.Reason : "PUT won the score"; return null; }
        reason = d.Reason;
        return d;
    }

    public RatedDirectionDecision Decide(string symbol, in RatedDirectionInput m) =>
        Decide(symbol, m, _settings.GetDecimal(MinScoreKey, symbol, DefaultMinScore),
            _settings.GetDecimal(MinMarginKey, symbol, DefaultMinMargin));

    /// <summary>Thresholds passed explicitly so the backtest can sweep them without settings rows.</summary>
    public RatedDirectionDecision Decide(string symbol, in RatedDirectionInput m, decimal minScore, decimal minMargin)
    {
        var call = Score(symbol, true, m);
        var put = Score(symbol, false, m);
        var (best, other, side) = call >= put ? (call, put, "CALL") : (put, call, "PUT");

        if (best < minScore)
            return new(call, put, null, $"best {side} {best:F0} < min {minScore:F0}");
        if (best - other < minMargin)
            return new(call, put, null, $"margin {best - other:F0} < min {minMargin:F0}");
        return new(call, put, side, $"{side} {best:F0} vs {other:F0}");
    }

    public decimal Score(string symbol, bool isCall, in RatedDirectionInput m)
    {
        decimal score = 0m;

        var emaAligned = isCall ? m.Ema20 > m.Ema50 : m.Ema20 < m.Ema50;
        var priceAligned = isCall ? m.Ltp > m.Ema20 : m.Ltp < m.Ema20;
        score += emaAligned && priceAligned ? 30m : emaAligned ? 15m : 0m;

        if (emaAligned)
        {
            var gap = Math.Abs(m.Ema20 - m.Ema50);
            var minGap = _settings.GetDecimal("EmaGapMinPoints", symbol, 10m);
            score += gap >= 2m * minGap ? 15m : gap > minGap ? 10m : 0m;
        }

        var favourable = isCall ? m.DayChangePct : -m.DayChangePct;
        score += favourable > 0.5m ? 12m : favourable > 0.2m ? 6m : 0m;
        if (isCall ? m.LastBarBullish : m.LastBarBearish)
            score += 8m;

        if (m.Pcr is { } pcr && pcr > 0)
        {
            var inBand = isCall
                ? pcr >= _settings.GetDecimal("CallPcrMinimum", symbol, 0.98m) && pcr <= _settings.GetDecimal("CallPcrMaximum", symbol, 1.5m)
                : pcr <= _settings.GetDecimal("PutPcrMaximum", symbol, 1.4m)
                  && (!_settings.IsSettingActive("PutPcrMinimum", symbol) || pcr >= _settings.GetDecimal("PutPcrMinimum", symbol, 0.7m));
            if (inBand) score += 20m;
        }

        if (m.AtrPct >= _settings.GetDecimal("MinAtrPercent", symbol, 0.03m))
            score += 15m;

        return score;
    }
}
