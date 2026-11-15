using Trading.Worker.SignalGates;

namespace Trading.Worker.Strategies;

/// <summary>
/// Per-instrument signal generation seam. SignalWorker asks
/// <see cref="InstrumentStrategyRegistry"/> for the strategy that owns a symbol and calls
/// <see cref="GenerateAsync"/>; everything before it (snapshot build, pre-gates) and after it
/// (post-gates, order placement) stays symbol-agnostic in the worker.
///
/// This exists so a single instrument can diverge WITHOUT forking the shared engines. Almost
/// every symbol should keep using <see cref="DefaultInstrumentStrategy"/>: instrument differences
/// that are only parameter differences (ATR/RSI/PCR thresholds, body%, lot size, strike interval)
/// already resolve per-symbol at runtime through IStrategySettingsService and the TradingInstrument
/// row, and tuning those needs no code change or redeploy. Write a dedicated implementation only
/// when a symbol needs genuinely DIFFERENT decision logic — not different numbers.
/// </summary>
public interface IInstrumentStrategy
{
    /// <summary>
    /// True when this strategy owns <paramref name="symbol"/>. Called on every signal cycle, so
    /// keep it a cheap comparison — no I/O.
    /// </summary>
    bool Handles(string symbol);

    /// <summary>
    /// Produces the signal decision for one cycle. Returning a result with a null Signal is the
    /// normal "no trade this cycle" outcome and must carry BlockedBy/BlockedReason so the
    /// IndicatorSnapshot audit row still explains the suppression.
    /// </summary>
    Task<SignalSelectionResult> GenerateAsync(
        SignalContext ctx,
        MarketSnapshot market,
        string symbol,
        CancellationToken ct);
}
