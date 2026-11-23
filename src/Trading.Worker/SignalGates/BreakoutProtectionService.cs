
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Worker.SignalGates
{
    public sealed class BreakoutProtectionService
    {
        /// <summary>
        /// StrategySettings flag (per symbol, then global). The check runs only when this row is on; with no row it is OFF,
        /// so it is switched on and off from the database like every other filter. The backtest reads the same key.
        /// </summary>
        internal const string EnableKey = "EnableBreakoutProtection";

        // All values are in index/futures points, not option premium.
        private const decimal BreakoutBuffer = 5m;
        private const decimal PullbackBuffer = 5m;

        private readonly ConcurrentDictionary<string, SymbolState> _states = new();

        /// <summary>
        /// Call on every market cycle, even when no trading signal exists.
        /// Processes both CALL and PUT candle states.
        /// </summary>
        public void Update(
            string symbol,
            IReadOnlyList<Candle>? candles)
        {
            if (string.IsNullOrWhiteSpace(symbol) ||
                candles == null ||
                candles.Count == 0)
                return;

            var sessionStartUtc = TradingConstants.TodayUtcStart();

            var todayCandles = candles
                .Where(c =>
                    c.CandleTime >= sessionStartUtc &&
                    c.High > 0 &&
                    c.Low > 0 &&
                    c.Close > 0)
                .OrderBy(c => c.CandleTime)
                .ToList();

            if (todayCandles.Count == 0)
                return;

            // Update both directions on every cycle.
            ProcessCandles(
                GetState(symbol, true, sessionStartUtc),
                todayCandles,
                true,
                sessionStartUtc);

            ProcessCandles(
                GetState(symbol, false, sessionStartUtc),
                todayCandles,
                false,
                sessionStartUtc);
        }

        /// <summary>
        /// Returns the latest decision for the requested direction.
        /// Update() should also be called on every market cycle.
        /// </summary>
        public BreakoutDecision Evaluate(
            string symbol,
            bool isCall,
            IReadOnlyList<Candle>? candles)
        {
            if (string.IsNullOrWhiteSpace(symbol))
                return BreakoutDecision.Block("Missing symbol");

            if (candles == null || candles.Count < 2)
                return BreakoutDecision.Block(
                    "Insufficient 1m candles for breakout protection");

            var sessionStartUtc = TradingConstants.TodayUtcStart();

            // Safe even if Update() was not separately called.
            Update(symbol, candles);

            var state = GetState(symbol, isCall, sessionStartUtc);

            lock (state)
            {
                return state.LastDecision;
            }
        }

        /// <summary>
        /// Backtest replay only (Trading.Backtest). Runs the SAME state machine and buffers as <see cref="Evaluate"/> over one
        /// session's closed 1m candles, in time order, on a fresh state that is not stored in the live per-symbol cache.
        /// result[i] is the decision the live service holds after candles[0..i] have been processed. Not called by the
        /// live pipeline; it changes no live behaviour.
        /// </summary>
        internal static BreakoutDecision[] ReplaySession(IReadOnlyList<Candle> sessionCandles, bool isCall)
        {
            var state = new SymbolState();
            state.Reset(default);

            var result = new BreakoutDecision[sessionCandles.Count];
            var one = new Candle[1];

            for (var i = 0; i < sessionCandles.Count; i++)
            {
                one[0] = sessionCandles[i];
                ProcessCandles(state, one, isCall, default);
                result[i] = state.LastDecision;
            }

            return result;
        }

        private SymbolState GetState(
            string symbol,
            bool isCall,
            DateTime sessionStartUtc)
        {
            var key = $"{symbol}_{(isCall ? "CALL" : "PUT")}";

            var state = _states.GetOrAdd(
                key,
                _ => new SymbolState());

            lock (state)
            {
                if (state.SessionStartUtc != sessionStartUtc)
                {
                    state.Reset(sessionStartUtc);
                }
            }

            return state;
        }

        private static void ProcessCandles(
            SymbolState state,
            IReadOnlyList<Candle> candles,
            bool isCall,
            DateTime sessionStartUtc)
        {
            lock (state)
            {
                if (state.SessionStartUtc != sessionStartUtc)
                    state.Reset(sessionStartUtc);

                foreach (var candle in candles)
                {
                    if (candle.CandleTime == default ||
                        candle.High <= 0 ||
                        candle.Low <= 0 ||
                        candle.Close <= 0)
                        continue;

                    // Process each closed candle exactly once.
                    if (candle.CandleTime <= state.LastProcessedCandleTime)
                        continue;

                    if (!state.Initialized)
                    {
                        // First available candle establishes the initial
                        // session high/low. It is not treated as a breakout.
                        state.CandleDayHigh = candle.High;
                        state.CandleDayLow = candle.Low;
                        state.Initialized = true;
                        state.LastProcessedCandleTime = candle.CandleTime;

                        state.LastDecision = BreakoutDecision.Block(
                            "Initializing candle-based session high/low");

                        continue;
                    }

                    // IMPORTANT:
                    // Capture previous session extremes BEFORE updating
                    // them with the current candle.
                    var previousDayHigh = state.CandleDayHigh;
                    var previousDayLow = state.CandleDayLow;

                    // Evaluate current candle against previous extremes.
                    state.LastDecision = isCall
                        ? EvaluateCall(state, candle, previousDayHigh)
                        : EvaluatePut(state, candle, previousDayLow);

                    // Update today's running extremes AFTER evaluation.
                    state.CandleDayHigh = Math.Max(
                        state.CandleDayHigh,
                        candle.High);

                    state.CandleDayLow = Math.Min(
                        state.CandleDayLow,
                        candle.Low);

                    state.LastProcessedCandleTime = candle.CandleTime;
                }
            }
        }

        private static BreakoutDecision EvaluateCall(
            SymbolState state,
            Candle candle,
            decimal previousDayHigh)
        {
            switch (state.Phase)
            {
                case BreakoutPhase.WaitingForBreakout:
                case BreakoutPhase.Failed:

                    // A fresh breakout must exceed the previous
                    // running session high.
                    if (candle.Close > previousDayHigh + BreakoutBuffer)
                    {
                        state.Phase = BreakoutPhase.PullbackTracking;
                        state.BreakoutLevel = previousDayHigh;
                        state.PullbackLow = candle.Low;
                        state.PullbackHigh = 0m;

                        return BreakoutDecision.Block(
                            $"CALL breakout detected above day high " +
                            $"{previousDayHigh:F2}; waiting for pullback");
                    }

                    return BreakoutDecision.Block(
                        $"CALL waiting for day-high breakout. " +
                        $"Candle day high={previousDayHigh:F2}");

                case BreakoutPhase.PullbackTracking:

                    if (candle.Close <
                        state.BreakoutLevel - PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.Failed;

                        return BreakoutDecision.Block(
                            "CALL failed breakout: closed below breakout level");
                    }

                    state.PullbackLow = Math.Min(
                        state.PullbackLow,
                        candle.Low);

                    // Price must return near the breakout level.
                    if (candle.Low <=
                        state.BreakoutLevel + PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.WaitingForContinuation;

                        // Start tracking the pullback swing high.
                        state.PullbackHigh = candle.High;

                        return BreakoutDecision.Block(
                            "CALL pullback detected; waiting for continuation");
                    }

                    return BreakoutDecision.Block(
                        "CALL breakout detected; waiting for pullback");

                case BreakoutPhase.WaitingForContinuation:

                    if (candle.Close <
                        state.BreakoutLevel - PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.Failed;

                        return BreakoutDecision.Block(
                            "CALL retest failed below breakout level");
                    }

                    // Check continuation against the PREVIOUS swing high
                    // before updating it with the current candle.
                    if (candle.Close > state.PullbackHigh &&
                        candle.Close > state.BreakoutLevel)
                    {
                        state.Phase = BreakoutPhase.ContinuationConfirmed;

                        return BreakoutDecision.Allow(
                            "CALL breakout, pullback and continuation confirmed");
                    }

                    // No continuation yet: update swing for next candle.
                    state.PullbackHigh = Math.Max(
                        state.PullbackHigh,
                        candle.High);

                    state.PullbackLow = Math.Min(
                        state.PullbackLow,
                        candle.Low);

                    return BreakoutDecision.Block(
                        "CALL waiting for close above pullback high");

                case BreakoutPhase.ContinuationConfirmed:

                    if (candle.Close <
                        state.BreakoutLevel - PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.Failed;

                        return BreakoutDecision.Block(
                            "CALL breakout invalidated");
                    }

                    return BreakoutDecision.Allow(
                        "CALL continuation remains valid");

                default:
                    return BreakoutDecision.Block(
                        "CALL breakout state unavailable");
            }
        }

        private static BreakoutDecision EvaluatePut(
            SymbolState state,
            Candle candle,
            decimal previousDayLow)
        {
            switch (state.Phase)
            {
                case BreakoutPhase.WaitingForBreakout:
                case BreakoutPhase.Failed:

                    // A fresh breakdown must go below the previous
                    // running session low.
                    if (candle.Close < previousDayLow - BreakoutBuffer)
                    {
                        state.Phase = BreakoutPhase.PullbackTracking;
                        state.BreakoutLevel = previousDayLow;
                        state.PullbackHigh = candle.High;
                        state.PullbackLow = 0m;

                        return BreakoutDecision.Block(
                            $"PUT breakdown detected below day low " +
                            $"{previousDayLow:F2}; waiting for pullback");
                    }

                    return BreakoutDecision.Block(
                        $"PUT waiting for day-low breakdown. " +
                        $"Candle day low={previousDayLow:F2}");

                case BreakoutPhase.PullbackTracking:

                    if (candle.Close >
                        state.BreakoutLevel + PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.Failed;

                        return BreakoutDecision.Block(
                            "PUT failed breakdown: closed above breakdown level");
                    }

                    state.PullbackHigh = Math.Max(
                        state.PullbackHigh,
                        candle.High);

                    // Price must rebound near the breakdown level.
                    if (candle.High >=
                        state.BreakoutLevel - PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.WaitingForContinuation;

                        // Start tracking the pullback swing low.
                        state.PullbackLow = candle.Low;

                        return BreakoutDecision.Block(
                            "PUT pullback detected; waiting for continuation");
                    }

                    return BreakoutDecision.Block(
                        "PUT breakdown detected; waiting for pullback");

                case BreakoutPhase.WaitingForContinuation:

                    if (candle.Close >
                        state.BreakoutLevel + PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.Failed;

                        return BreakoutDecision.Block(
                            "PUT retest failed above breakdown level");
                    }

                    // Check continuation against the PREVIOUS swing low
                    // before updating it with the current candle.
                    if (candle.Close < state.PullbackLow &&
                        candle.Close < state.BreakoutLevel)
                    {
                        state.Phase = BreakoutPhase.ContinuationConfirmed;

                        return BreakoutDecision.Allow(
                            "PUT breakdown, pullback and continuation confirmed");
                    }

                    // No continuation yet: update swing for next candle.
                    state.PullbackLow = Math.Min(
                        state.PullbackLow,
                        candle.Low);

                    state.PullbackHigh = Math.Max(
                        state.PullbackHigh,
                        candle.High);

                    return BreakoutDecision.Block(
                        "PUT waiting for close below pullback low");

                case BreakoutPhase.ContinuationConfirmed:

                    if (candle.Close >
                        state.BreakoutLevel + PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.Failed;

                        return BreakoutDecision.Block(
                            "PUT breakdown invalidated");
                    }

                    return BreakoutDecision.Allow(
                        "PUT continuation remains valid");

                default:
                    return BreakoutDecision.Block(
                        "PUT breakout state unavailable");
            }
        }

        private sealed class SymbolState
        {
            public DateTime SessionStartUtc { get; set; }

            public DateTime LastProcessedCandleTime { get; set; }

            public bool Initialized { get; set; }

            public decimal CandleDayHigh { get; set; }

            public decimal CandleDayLow { get; set; }

            public decimal BreakoutLevel { get; set; }

            public decimal PullbackHigh { get; set; }

            public decimal PullbackLow { get; set; }

            public BreakoutPhase Phase { get; set; } =
                BreakoutPhase.WaitingForBreakout;

            public BreakoutDecision LastDecision { get; set; } =
                BreakoutDecision.Block("Waiting for session candles");

            public void Reset(DateTime sessionStartUtc)
            {
                SessionStartUtc = sessionStartUtc;
                LastProcessedCandleTime = default;
                Initialized = false;

                CandleDayHigh = 0m;
                CandleDayLow = 0m;

                BreakoutLevel = 0m;
                PullbackHigh = 0m;
                PullbackLow = decimal.MaxValue;

                Phase = BreakoutPhase.WaitingForBreakout;

                LastDecision = BreakoutDecision.Block(
                    "Waiting for session candles");
            }
        }

        private enum BreakoutPhase
        {
            WaitingForBreakout,
            PullbackTracking,
            WaitingForContinuation,
            ContinuationConfirmed,
            Failed
        }
    }

    public sealed record BreakoutDecision(bool Allowed, string Reason)
    {
        public static BreakoutDecision Allow(string reason) =>
            new(true, reason);

        public static BreakoutDecision Block(string reason) =>
            new(false, reason);
    }
}
