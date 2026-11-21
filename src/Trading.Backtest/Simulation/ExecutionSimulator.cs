
using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Settings;
using Trading.Domain;
using Trading.Worker.SignalGates;

namespace Trading.Backtest.Simulation;

/// <summary>One symbol's rebuilt decision points plus the 1m bars its trades are replayed on.</summary>
public sealed class SymbolData
{
    public required string Symbol { get; init; }
    public required MarketPoint[] Points { get; init; }
    public required Bar[] Bars { get; init; }
    public required int StrikeInterval { get; init; }
}

public enum PremiumMode
{
    /// <summary>Index move x average delta for every trade.</summary>
    ModelOnly,

    /// <summary>Stored option prices where they cover the trade, else the model.</summary>
    OptionHistoryWhereAvailable,
}

public sealed record SimulationOptions(
    TradeSide Side,
    StopModel StopModel,
    PremiumMode PremiumMode,
    CostModel? Costs,
    Func<MarketPoint, bool>? RejectEntry = null,
    int? MaxTradesPerSymbolDay = null,
    int MaxConcurrentPositions = 1,
    bool PerSymbolSlots = false,
    bool DisableHandoffCooldown = false,
    int? KeepEveryNth = null,
    bool BothSides = false,
    bool LogSignals = false);

/// <summary>
/// Per-signal execution log.
/// </summary>
public sealed record SignalLogRow(
    DateTime TimeIst,
    string Symbol,
    string Side,
    string Path,
    string Outcome,
    string OpenSymbol,
    DateTime? HeldUntilIst,
    DateTime? ExitIst,
    string ExitReason,
    double? PnlRs,
    double? CostRs);

/// <summary>
/// Records every EntryEvaluator decision, including decisions
/// where no signal was produced.
/// </summary>
public sealed record EvaluationLogRow(
    DateTime TimeIst,
    string Symbol,
    string Side,
    string Path,
    bool SignalProduced,
    bool Candidate,
    string BlockedBy,
    string Reason);

public sealed class SimulationResult
{
    public List<Trade> Trades { get; } = [];

    public List<SignalLogRow> Log { get; } = [];

    // NEW: one row for every evaluator decision.
    public List<EvaluationLogRow> Evaluations { get; } = [];

    public int TotalSignals { get; set; }

    public Dictionary<string, int> BlockedBy { get; } = new();

    public int BlockedCount => BlockedBy.Values.Sum();

    public int DataUnavailable { get; set; }

    public int OiUnverifiable { get; set; }

    public int RealPremiumTrades =>
        Trades.Count(t => t.Source == PremiumSource.OptionMinuteHistory);
}

/// <summary>
/// Chronological replay across symbols.
/// </summary>
public sealed class ExecutionSimulator
{
    private readonly EntryEvaluator _entry;
    private readonly BacktestSettings _settings;
    private readonly ExitModel _exit;
    private readonly OptionHistoryIndex? _options;

    public ExecutionSimulator(
        EntryEvaluator entry,
        BacktestSettings settings,
        ExitModel exit,
        OptionHistoryIndex? options)
    {
        _entry = entry;
        _settings = settings;
        _exit = exit;
        _options = options;
    }

    public SimulationResult Run(
        IReadOnlyList<SymbolData> data,
        SimulationOptions opt)
    {
        var result = new SimulationResult();

        var sides = opt.BothSides
            ? new[] { TradeSide.Call, TradeSide.Put }
            : new[] { opt.Side };

        var timeline = data
            .SelectMany((d, di) =>
                d.Points.Select(p => (Point: p, Data: d, Order: di)))
            .OrderBy(x => x.Point.TimeUtc)
            .ThenBy(x => x.Order)
            .ToList();

        var open = new List<(DateTime ExitUtc, string Symbol)>();

        (DateTime Utc, string Symbol, string Reason)? lastExitAny = null;

        var lastExitSameSide =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        var rejectedAt =
            new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        var tradesPerSymbolDay =
            new Dictionary<(string, DateTime), int>();

        var validEntrySeen = 0;

        foreach (var (p, d, _) in timeline)
            foreach (var side in sides)
            {
                var signalType = side == TradeSide.Call
                    ? TradingConst.SignalType.CALL
                    : TradingConst.SignalType.PUT;

                // Evaluate using the existing EntryEvaluator.
                var verdict = _entry.Evaluate(p, side);

                // NEW: log every evaluator decision.
                // This runs before any execution gates.
                if (opt.LogSignals)
                {
                    result.Evaluations.Add(new EvaluationLogRow(
                        p.TimeIst,
                        p.Symbol,
                        side.ToString().ToUpperInvariant(),
                        verdict.Path,
                        verdict.SignalProduced,
                        verdict.Candidate,
                        verdict.BlockedBy ?? "",
                        verdict.Reason ?? ""
                    ));
                }

                void Rej(
                    string gate,
                    DateTime? heldUntilUtc = null,
                    string openSymbol = "")
                {
                    Block(result, gate);

                    if (opt.LogSignals)
                    {
                        result.Log.Add(new SignalLogRow(
                            p.TimeIst,
                            p.Symbol,
                            side.ToString().ToUpperInvariant(),
                            verdict.Path,
                            gate,
                            openSymbol,
                            heldUntilUtc.HasValue
                                ? heldUntilUtc.Value + SessionClock.IstOffset
                                : null,
                            null,
                            "",
                            null,
                            null
                        ));
                    }
                }

                if (!verdict.SignalProduced)
                {
                    if (verdict.BlockedBy?.StartsWith(
                        "DataUnavailable",
                        StringComparison.Ordinal) == true)
                    {
                        result.DataUnavailable++;
                    }

                    continue;
                }

                result.TotalSignals++;

                if (!verdict.Candidate)
                {
                    Rej(verdict.BlockedBy ?? "UnknownEntryGate");
                    continue;
                }

                var symbol = p.Symbol;
                var nowIst = p.TimeIst;

                if (opt.MaxTradesPerSymbolDay is { } cap &&
                    tradesPerSymbolDay.GetValueOrDefault(
                        (symbol, p.TimeIst.Date)) >= cap)
                {
                    Rej("MaxTradesPerSymbolDay");
                    continue;
                }

                // OnePositionGate.
                open.RemoveAll(o => o.ExitUtc <= p.TimeUtc);

                var inUse = opt.PerSymbolSlots
                    ? open.Count(o => string.Equals(
                        o.Symbol,
                        symbol,
                        StringComparison.OrdinalIgnoreCase))
                    : open.Count;

                if (inUse >= opt.MaxConcurrentPositions)
                {
                    Rej(
                        "OnePositionGate",
                        open.Min(o => o.ExitUtc),
                        string.Join('+', open.Select(o => o.Symbol)));

                    continue;
                }

                // OI confirmation.
                if (_settings.GetBool(
                    "EnableOiConfirmationGate", symbol, true) &&
                    _options is not null)
                {
                    var prevClose = p.NextBarIndex >= 2
                        ? d.Bars[p.NextBarIndex - 2].Close
                        : p.LastClosedBar.Close;

                    var confirms = _options.OiConfirms(
                        symbol,
                        side,
                        nowIst,
                        d.StrikeInterval,
                        p.LastClosedBar.Close - prevClose);

                    if (confirms is false)
                    {
                        Rej("OiConfirmationGate");
                        continue;
                    }

                    if (confirms is null)
                        result.OiUnverifiable++;
                }
                else if (_settings.GetBool(
                    "EnableOiConfirmationGate", symbol, true))
                {
                    result.OiUnverifiable++;
                }

                // DuplicateSignalGate.
                var key = $"{symbol}|{side}";

                var rejectedCooldown = _settings.GetInt(
                    "RejectedEntryCooldownMinutes", symbol, 5);

                if (rejectedCooldown > 0 &&
                    rejectedAt.TryGetValue(key, out var rej) &&
                    p.TimeUtc - rej < TimeSpan.FromMinutes(rejectedCooldown))
                {
                    Rej(
                        "DuplicateSignalGate",
                        rej + TimeSpan.FromMinutes(rejectedCooldown));

                    continue;
                }

                if (lastExitSameSide.TryGetValue(key, out var lastClose) &&
                    p.TimeUtc - lastClose < TimeSpan.FromMinutes(5))
                {
                    Rej(
                        "DuplicateSignalGate",
                        lastClose + TimeSpan.FromMinutes(5));

                    continue;
                }

                // HandoffCooldownGate.
                if (!opt.DisableHandoffCooldown &&
                    _settings.GetBool(
                        HandoffCooldownGate.EnableKey, symbol, false))
                {
                    var minutes = _settings.GetInt(
                        HandoffCooldownGate.MinutesKey,
                        symbol,
                        HandoffCooldownGate.DefaultMinutes);

                    if (minutes > 0 &&
                        lastExitAny is { } le &&
                        HandoffCooldownGate.Evaluate(
                            signalType,
                            le.Symbol,
                            le.Utc + SessionClock.IstOffset,
                            le.Reason,
                            nowIst,
                            minutes) is not null)
                    {
                        Rej(
                            "HandoffCooldownGate",
                            le.Utc + TimeSpan.FromMinutes(minutes));

                        continue;
                    }
                }

                // Injected rejection for tests.
                if (opt.RejectEntry?.Invoke(p) == true)
                {
                    rejectedAt[key] = p.TimeUtc;
                    Rej("OrderRejected");
                    continue;
                }

                // Deterministic thinning.
                if (opt.KeepEveryNth is { } nth &&
                    nth > 1 &&
                    validEntrySeen++ % nth != 0)
                {
                    Rej("KeepEveryNth");
                    continue;
                }

                var trade = Execute(p, d, opt, side, verdict.Path);

                if (trade is null)
                {
                    Rej("NoPathAfterEntry");
                    continue;
                }

                result.Trades.Add(trade);

                if (opt.LogSignals)
                {
                    result.Log.Add(new SignalLogRow(
                        p.TimeIst,
                        symbol,
                        side.ToString().ToUpperInvariant(),
                        verdict.Path,
                        "ENTERED",
                        "",
                        null,
                        trade.ExitUtc + SessionClock.IstOffset,
                        trade.ExitReason,
                        trade.PnlRs,
                        trade.CostRs
                    ));
                }

                tradesPerSymbolDay[(symbol, p.TimeIst.Date)] =
                    tradesPerSymbolDay.GetValueOrDefault(
                        (symbol, p.TimeIst.Date)) + 1;

                open.Add((trade.ExitUtc, symbol));

                lastExitAny = (
                    trade.ExitUtc,
                    symbol,
                    trade.ExitReason);

                lastExitSameSide[key] = trade.ExitUtc;
            }

        return result;
    }

    private static void Block(SimulationResult r, string gate)
    {
        r.BlockedBy[gate] =
            r.BlockedBy.GetValueOrDefault(gate) + 1;
    }

    private Trade? Execute(
        MarketPoint p,
        SymbolData d,
        SimulationOptions opt,
        TradeSide side,
        string entryPath)
    {
        var sign = side == TradeSide.Call ? 1.0 : -1.0;

        var cfg = _exit.Symbols[p.Symbol];

        var endUtc = p.TimeUtc.Date + SessionClock.EntryEndUtc;

        var indexMoves = new List<double>();
        var closeTimes = new List<DateTime>();

        for (var q = p.NextBarIndex; q < d.Bars.Length; q++)
        {
            var bar = d.Bars[q];

            if (bar.OpenUtc.Date != p.TimeUtc.Date ||
                bar.OpenUtc >= endUtc)
            {
                break;
            }

            indexMoves.Add(sign * (bar.Close - p.Ltp));
            closeTimes.Add(bar.OpenUtc.AddMinutes(1));
        }

        if (indexMoves.Count == 0)
            return null;

        var source = PremiumSource.IndexDeltaModel;
        var delta = cfg.Delta;
        var entryPremium = cfg.EntryPremium;

        double[] path = indexMoves
            .Select(m => m * delta)
            .ToArray();

        if (opt.PremiumMode ==
                PremiumMode.OptionHistoryWhereAvailable &&
            _options is not null)
        {
            var real = _options.TryPath(
                p.Symbol,
                side,
                p.TimeIst,
                closeTimes
                    .Select(c => c + SessionClock.IstOffset)
                    .ToList());

            if (real is not null)
            {
                source = PremiumSource.OptionMinuteHistory;
                delta = real.Delta > 0 ? real.Delta : delta;
                entryPremium = real.EntryPremium;
                path = real.Moves;
            }
        }

        var r = _exit.IndexStopPoints(p.Atr) * delta;

        var local = cfg with
        {
            Delta = delta,
            EntryPremium = entryPremium
        };

        var replay = ExitReplay.Run(
            path,
            r,
            p.Atr,
            local,
            _exit,
            opt.StopModel,
            side == TradeSide.Call);

        var pnl = replay.PnlPoints * cfg.LotSize;

        double? cost = opt.Costs is { } c
            ? c.Cost(replay.Orders, cfg.LotSize, _exit.Lots)
            : null;

        var exitUtc = closeTimes[
            Math.Min(replay.ExitStep, closeTimes.Count - 1)];

        return new Trade(
            p.Symbol,
            side,
            p.TimeUtc,
            exitUtc,
            entryPath,
            source,
            entryPremium,
            r,
            pnl,
            cost,
            replay.ExitReason,
            replay.Orders,
            replay.ReachedT1,
            replay.ReachedMid,
            replay.ReachedFinal,
            Entry: new EntryContext(
                p.Rsi1m,
                p.Rsi5m,
                p.Pcr,
                p.AtrPct,
                p.OpenGapPct,
                p.DayChangePct,
                StretchAtr: p.Atr > 0 && p.Ema20 > 0
                    ? (side == TradeSide.Call ? 1.0 : -1.0) *
                      (p.Ltp - p.Ema20) / p.Atr
                    : 0,
                Efficiency: p.DayHigh - p.DayLow > 0 &&
                            p.SessionOpen > 0
                    ? Math.Abs(p.Ltp - p.SessionOpen) /
                      (p.DayHigh - p.DayLow)
                    : null));
    }
}
