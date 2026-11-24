
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
        internal const string EnableKey = "EnableBreakoutProtection";

        // All values are index/futures points.
        private const decimal BreakoutBuffer = 5m;
        private const decimal PullbackBuffer = 5m;

        private readonly ConcurrentDictionary<string, SymbolState> _states = new();

        public void Update(string symbol, IReadOnlyList<Candle>? candles)
        {
            if (string.IsNullOrWhiteSpace(symbol) ||
                candles == null || candles.Count == 0)
                return;

            var sessionStartUtc = TradingConstants.TodayUtcStart();

            var todayCandles = candles
                .Where(c =>
                    c.CandleTime >= sessionStartUtc &&
                    c.High > 0 && c.Low > 0 && c.Close > 0)
                .OrderBy(c => c.CandleTime)
                .ToList();

            if (todayCandles.Count == 0)
                return;

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

            Update(symbol, candles);

            var state = GetState(symbol, isCall, sessionStartUtc);

            lock (state)
                return state.LastDecision;
        }

        internal static BreakoutDecision[] ReplaySession(
            IReadOnlyList<Candle> sessionCandles,
            bool isCall)
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
                key, _ => new SymbolState());

            lock (state)
            {
                if (state.SessionStartUtc != sessionStartUtc)
                    state.Reset(sessionStartUtc);
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
                        candle.Close <= 0 ||
                        candle.High < candle.Low)
                        continue;

                    if (candle.CandleTime <= state.LastProcessedCandleTime)
                        continue;

                    if (!state.Initialized)
                    {
                        state.CandleDayHigh = candle.High;
                        state.CandleDayLow = candle.Low;
                        state.Initialized = true;
                        state.LastProcessedCandleTime = candle.CandleTime;

                        state.LastDecision = BreakoutDecision.Block(
                            "Initializing session high/low");

                        continue;
                    }

                    var previousDayHigh = state.CandleDayHigh;
                    var previousDayLow = state.CandleDayLow;

                    state.LastDecision = isCall
                        ? EvaluateCall(state, candle, previousDayHigh)
                        : EvaluatePut(state, candle, previousDayLow);

                    state.CandleDayHigh = Math.Max(
                        state.CandleDayHigh, candle.High);

                    state.CandleDayLow = Math.Min(
                        state.CandleDayLow, candle.Low);

                    state.LastProcessedCandleTime = candle.CandleTime;
                }
            }
        }

        private static BreakoutDecision EvaluateCall(
            SymbolState state,
            Candle candle,
            decimal previousDayHigh)
        {
            // A fresh higher breakout can replace an older setup.
            var freshBreakout =
                candle.High > previousDayHigh + BreakoutBuffer;

            switch (state.Phase)
            {
                case BreakoutPhase.WaitingForBreakout:
                case BreakoutPhase.Failed:

                    if (freshBreakout &&
                        candle.Close > previousDayHigh + BreakoutBuffer)
                    {
                        StartCallSetup(state, previousDayHigh, candle);

                        return BreakoutDecision.Block(
                            $"CALL breakout confirmed; level={previousDayHigh:F2}; waiting for pullback");
                    }

                    return BreakoutDecision.Block(
                        freshBreakout
                            ? "CALL breakout wick detected; waiting for close confirmation"
                            : $"CALL waiting for day-high breakout. High={previousDayHigh:F2}");

                case BreakoutPhase.PullbackTracking:

                    if (freshBreakout &&
                        candle.Close > previousDayHigh + BreakoutBuffer)
                    {
                        StartCallSetup(state, previousDayHigh, candle);

                        return BreakoutDecision.Block(
                            $"CALL fresh higher breakout; tracking new level={previousDayHigh:F2}");
                    }

                    if (candle.Close <
                        state.BreakoutLevel - PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.Failed;

                        return BreakoutDecision.Block(
                            "CALL failed breakout: closed below protected level");
                    }

                    state.PullbackLow = Math.Min(
                        state.PullbackLow, candle.Low);

                    if (candle.Low <=
                        state.BreakoutLevel + PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.WaitingForContinuation;
                        state.PullbackHigh = candle.High;

                        return BreakoutDecision.Block(
                            "CALL pullback detected; waiting for continuation");
                    }

                    return BreakoutDecision.Block(
                        "CALL waiting for pullback");

                case BreakoutPhase.WaitingForContinuation:

                    if (freshBreakout &&
                        candle.Close > previousDayHigh + BreakoutBuffer)
                    {
                        StartCallSetup(state, previousDayHigh, candle);

                        return BreakoutDecision.Block(
                            $"CALL fresh higher breakout; tracking new level={previousDayHigh:F2}");
                    }

                    if (candle.Close <
                        state.BreakoutLevel - PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.Failed;

                        return BreakoutDecision.Block(
                            "CALL retest failed below breakout level");
                    }

                    // Compare with the previous pullback swing high.
                    if (candle.Close > state.PullbackHigh &&
                        candle.Close > state.BreakoutLevel)
                    {
                        state.Phase = BreakoutPhase.ContinuationConfirmed;

                        return BreakoutDecision.Allow(
                            "CALL breakout, pullback and continuation confirmed");
                    }

                    state.PullbackHigh = Math.Max(
                        state.PullbackHigh, candle.High);

                    state.PullbackLow = Math.Min(
                        state.PullbackLow, candle.Low);

                    return BreakoutDecision.Block(
                        "CALL waiting for close above pullback high");

                case BreakoutPhase.ContinuationConfirmed:

                    if (freshBreakout &&
                        candle.Close > previousDayHigh + BreakoutBuffer)
                    {
                        // A new leg starts a fresh confirmation cycle.
                        StartCallSetup(state, previousDayHigh, candle);

                        return BreakoutDecision.Block(
                            $"CALL new breakout leg; tracking level={previousDayHigh:F2}");
                    }

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
            var freshBreakdown =
                candle.Low < previousDayLow - BreakoutBuffer;

            switch (state.Phase)
            {
                case BreakoutPhase.WaitingForBreakout:
                case BreakoutPhase.Failed:

                    if (freshBreakdown &&
                        candle.Close < previousDayLow - BreakoutBuffer)
                    {
                        StartPutSetup(state, previousDayLow, candle);

                        return BreakoutDecision.Block(
                            $"PUT breakdown confirmed; level={previousDayLow:F2}; waiting for pullback");
                    }

                    return BreakoutDecision.Block(
                        freshBreakdown
                            ? "PUT breakdown wick detected; waiting for close confirmation"
                            : $"PUT waiting for day-low breakdown. Low={previousDayLow:F2}");

                case BreakoutPhase.PullbackTracking:

                    if (freshBreakdown &&
                        candle.Close < previousDayLow - BreakoutBuffer)
                    {
                        StartPutSetup(state, previousDayLow, candle);

                        return BreakoutDecision.Block(
                            $"PUT fresh lower breakdown; tracking new level={previousDayLow:F2}");
                    }

                    if (candle.Close >
                        state.BreakoutLevel + PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.Failed;

                        return BreakoutDecision.Block(
                            "PUT failed breakdown: closed above protected level");
                    }

                    state.PullbackHigh = Math.Max(
                        state.PullbackHigh, candle.High);

                    if (candle.High >=
                        state.BreakoutLevel - PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.WaitingForContinuation;
                        state.PullbackLow = candle.Low;

                        return BreakoutDecision.Block(
                            "PUT pullback detected; waiting for continuation");
                    }

                    return BreakoutDecision.Block(
                        "PUT waiting for pullback");

                case BreakoutPhase.WaitingForContinuation:

                    if (freshBreakdown &&
                        candle.Close < previousDayLow - BreakoutBuffer)
                    {
                        StartPutSetup(state, previousDayLow, candle);

                        return BreakoutDecision.Block(
                            $"PUT fresh lower breakdown; tracking new level={previousDayLow:F2}");
                    }

                    if (candle.Close >
                        state.BreakoutLevel + PullbackBuffer)
                    {
                        state.Phase = BreakoutPhase.Failed;

                        return BreakoutDecision.Block(
                            "PUT retest failed above breakdown level");
                    }

                    // Compare with the previous pullback swing low.
                    if (candle.Close < state.PullbackLow &&
                        candle.Close < state.BreakoutLevel)
                    {
                        state.Phase = BreakoutPhase.ContinuationConfirmed;

                        return BreakoutDecision.Allow(
                            "PUT breakdown, pullback and continuation confirmed");
                    }

                    state.PullbackLow = Math.Min(
                        state.PullbackLow, candle.Low);

                    state.PullbackHigh = Math.Max(
                        state.PullbackHigh, candle.High);

                    return BreakoutDecision.Block(
                        "PUT waiting for close below pullback low");

                case BreakoutPhase.ContinuationConfirmed:

                    if (freshBreakdown &&
                        candle.Close < previousDayLow - BreakoutBuffer)
                    {
                        StartPutSetup(state, previousDayLow, candle);

                        return BreakoutDecision.Block(
                            $"PUT new breakdown leg; tracking level={previousDayLow:F2}");
                    }

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

        private static void StartCallSetup(
            SymbolState state,
            decimal level,
            Candle candle)
        {
            state.Phase = BreakoutPhase.PullbackTracking;
            state.BreakoutLevel = level;
            state.PullbackLow = candle.Low;
            state.PullbackHigh = 0m;
        }

        private static void StartPutSetup(
            SymbolState state,
            decimal level,
            Candle candle)
        {
            state.Phase = BreakoutPhase.PullbackTracking;
            state.BreakoutLevel = level;
            state.PullbackHigh = candle.High;
            state.PullbackLow = decimal.MaxValue;
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
