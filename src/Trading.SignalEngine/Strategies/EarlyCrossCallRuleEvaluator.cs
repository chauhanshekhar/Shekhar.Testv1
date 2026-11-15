using Trading.Application.Interfaces;
using Trading.Domain.Entities;

namespace Trading.SignalEngine.Strategies;

/// <summary>Everything <see cref="EarlyCrossCallRuleEvaluator"/> reads; built once per cycle by StrategyManager.</summary>
public readonly record struct EarlyCrossInput(
    bool IsPaper,
    int? BarsSince1mUp,
    decimal Ltp,
    decimal Ema20,
    decimal Ema50,
    decimal Ema20_5m,
    decimal Ema50_5m,
    decimal? Rsi5m,
    bool LastBarBullish);

/// <summary>
/// CALL entry in the first minutes after the 1m EMA20 rose above EMA50 (buy the turn, not the established move).
/// UNVALIDATED LIVE: backed only by a replay (docs/research/2026-10-27-early-cross). Therefore OFF by default
/// (EnableEarlyCrossCall) and PAPER ONLY, enforced here: any other TradingMode returns no match.
/// Decides ELIGIBILITY ONLY, like <see cref="SingleSignalRuleEvaluator"/>: option leg, SL/T1/T2, lot sizing, every gate,
/// duplicate protection and the exit flow still run.
/// </summary>
public class EarlyCrossCallRuleEvaluator
{
    public const string RuleId = "EARLY_CROSS_CALL";

    /// <summary>Written to Signal.SelectedStrategy so these trades can be separated without a schema change.</summary>
    public const string StrategyTag = "EarlyCross";

    public const string EnableKey = "EnableEarlyCrossCall";
    public const string MaxBarsKey = "EarlyCrossMaxBars";
    public const string Require5mNotUpKey = "EarlyCrossRequire5mNotUp";
    public const string RequireEmaGapKey = "EarlyCrossRequireEmaGap";
    public const int DefaultMaxBars = 10;

    private readonly IStrategySettingsService _settings;

    public EarlyCrossCallRuleEvaluator(IStrategySettingsService settings) => _settings = settings;

    public bool IsEnabled(string symbol) => _settings.GetBool(EnableKey, symbol, false);

    public SingleSignalRuleResult Evaluate(string symbol, in EarlyCrossInput e)
    {
        if (!IsEnabled(symbol))
            return SingleSignalRuleResult.NoMatch("EarlyCross disabled");
        if (!e.IsPaper)
            return SingleSignalRuleResult.NoMatch("EarlyCross is paper-only");

        var maxBars = _settings.GetInt(MaxBarsKey, symbol, DefaultMaxBars);
        if (e.BarsSince1mUp is not { } bars || bars > maxBars)
            return SingleSignalRuleResult.NoMatch("EarlyCross: 1m trend did not turn up within the allowed minutes");
        if (!(e.Ltp > e.Ema20 && e.Ema20 > e.Ema50))
            return SingleSignalRuleResult.NoMatch("EarlyCross: price/EMA alignment");
        if (!e.LastBarBullish)
            return SingleSignalRuleResult.NoMatch("EarlyCross: last 1m bar is not bullish");

        // Same band the CALL chain uses (condition 5); null RSI fails closed.
        var floor = _settings.GetDecimal("CallRsiFloor", symbol, 45m);
        var ceiling = _settings.GetDecimal("CallRsiCeiling", symbol, 70m);
        if (e.Rsi5m is not { } rsi || rsi < floor || rsi > ceiling)
            return SingleSignalRuleResult.NoMatch("EarlyCross: 5m RSI outside the CALL band");

        if (_settings.GetBool(Require5mNotUpKey, symbol, true)
            && e.Ema20_5m > 0 && e.Ema50_5m > 0 && e.Ema20_5m > e.Ema50_5m)
            return SingleSignalRuleResult.NoMatch("EarlyCross: 5m trend already up");

        if (_settings.GetBool(RequireEmaGapKey, symbol, true)
            && e.Ema20 - e.Ema50 < _settings.GetDecimal("EmaGapMinPoints", symbol, 10m))
            return SingleSignalRuleResult.NoMatch("EarlyCross: EMA gap below the minimum");

        return SingleSignalRuleResult.ForCall(RuleId);
    }

    /// <summary>
    /// Closed 1m bars since EMA20 first rose above EMA50 (0 = first bar above), from the closed candles in the snapshot.
    /// Null when EMA20 is not above EMA50 now, or the run covers the whole window (start unknown, never "early").
    /// Incremental EMAs over the window: within a small tolerance of the production EMA, which is why the early window is
    /// a count of bars, not a price level.
    /// </summary>
    public static int? BarsSince1mUp(IReadOnlyList<Candle> candles1m)
    {
        if (candles1m.Count < 60)
            return null;

        const decimal a20 = 2m / 21m;
        const decimal a50 = 2m / 51m;
        var e20 = candles1m[0].Close;
        var e50 = candles1m[0].Close;
        var run = 0;
        var runStartsAtWindowStart = true;

        for (var i = 1; i < candles1m.Count; i++)
        {
            e20 += a20 * (candles1m[i].Close - e20);
            e50 += a50 * (candles1m[i].Close - e50);
            if (e20 > e50)
                run++;
            else
            {
                run = 0;
                runStartsAtWindowStart = false;
            }
        }

        return run == 0 || runStartsAtWindowStart ? null : run - 1;
    }
}
