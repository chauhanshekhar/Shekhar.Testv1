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
    /// <summary>Stored option prices where they cover the trade, else the model; each trade says which.</summary>
    OptionHistoryWhereAvailable,
}

public sealed record SimulationOptions(
    TradeSide Side, StopModel StopModel, PremiumMode PremiumMode, CostModel? Costs,
    /// <summary>Rejected-entry events are not in history; a test can inject them to exercise the cooldown.</summary>
    Func<MarketPoint, bool>? RejectEntry = null,
    /// <summary>Cap on trades per symbol per day (null = none).</summary>
    int? MaxTradesPerSymbolDay = null,
    /// <summary>
    /// How many positions may be open at once. Production is 1 across ALL symbols (OnePositionGate),
    /// which is the default here. Raising it tests whether that single slot is costing opportunities.
    /// </summary>
    int MaxConcurrentPositions = 1,
    /// <summary>When true the cap applies per symbol instead of across all of them.</summary>
    bool PerSymbolSlots = false,
    /// <summary>Force the handoff cooldown off; it otherwise serialises entries regardless of slots.</summary>
    bool DisableHandoffCooldown = false,
    /// <summary>
    /// CONTROL ARM. Take only every Nth otherwise-valid entry. Deterministic and carries no market
    /// information, so it isolates how much of a filter's benefit is just "fewer trades, fewer costs".
    /// </summary>
    int? KeepEveryNth = null,
    /// <summary>
    /// DIAGNOSTIC. Evaluate CALL and PUT on every decision minute against ONE shared position slot and shared cooldowns,
    /// as production does (OnePositionGate counts a CALL and a PUT together). When both sides qualify on the same minute
    /// CALL is tried first; production ranks by confidence, which the replay does not model.
    /// </summary>
    bool BothSides = false,
    /// <summary>DIAGNOSTIC. Record one row per signal minute (accepted or rejected, what held it, when it frees up) in SimulationResult.Log.</summary>
    bool LogSignals = false);

/// <summary>One signal minute and what happened to it (only filled when LogSignals is on).</summary>
public sealed record SignalLogRow(
    DateTime TimeIst, string Symbol, string Side, string Path, string Outcome,
    string OpenSymbol, DateTime? HeldUntilIst, DateTime? ExitIst, string ExitReason, double? PnlRs, double? CostRs);

public sealed class SimulationResult
{
    public List<Trade> Trades { get; } = [];
    /// <summary>Per-signal log (LogSignals only): ENTERED, or the gate that rejected it; HeldUntilIst = when the position/cooldown that blocked it ends.</summary>
    public List<SignalLogRow> Log { get; } = [];
    /// <summary>Minutes where the CALL/PUT chain (or the approved rule) produced a signal, before any gate.</summary>
    public int TotalSignals { get; set; }
    /// <summary>Signal minutes stopped by a gate, by gate name. Invariant: TotalSignals = BlockedCount + Trades.Count.</summary>
    public Dictionary<string, int> BlockedBy { get; } = new();
    public int BlockedCount => BlockedBy.Values.Sum();
    /// <summary>Minutes where only PCR was missing (no snapshot): not judged, not counted as signals.</summary>
    public int DataUnavailable { get; set; }
    /// <summary>OI gate could not be replayed (no option history); the signal passed through.</summary>
    public int OiUnverifiable { get; set; }
    public int RealPremiumTrades => Trades.Count(t => t.Source == PremiumSource.OptionMinuteHistory);
}

/// <summary>
/// Chronological replay across the given symbols. One position is open at a time across ALL of them (OnePositionGate);
/// after an exit the HandoffCooldownGate blocks any new entry for its configured minutes, and DuplicateSignalGate blocks the
/// same symbol and direction for 5 minutes after a close and for RejectedEntryCooldownMinutes after a rejected entry.
/// One trade outcome is never reused for another configuration: each simulation re-runs entries and exits from the data.
/// </summary>
public sealed class ExecutionSimulator
{
    private readonly EntryEvaluator _entry;
    private readonly BacktestSettings _settings;
    private readonly ExitModel _exit;
    private readonly OptionHistoryIndex? _options;

    public ExecutionSimulator(EntryEvaluator entry, BacktestSettings settings, ExitModel exit, OptionHistoryIndex? options)
    {
        _entry = entry;
        _settings = settings;
        _exit = exit;
        _options = options;
    }

    public SimulationResult Run(IReadOnlyList<SymbolData> data, SimulationOptions opt)
    {
        var result = new SimulationResult();
        var sides = opt.BothSides ? new[] { TradeSide.Call, TradeSide.Put } : new[] { opt.Side };

        var timeline = data
            .SelectMany((d, di) => d.Points.Select(p => (Point: p, Data: d, Order: di)))
            .OrderBy(x => x.Point.TimeUtc).ThenBy(x => x.Order)
            .ToList();

        // Open positions as (exit time, symbol). Production keeps at most one of these; the
        // slot options below are the only thing that lets it hold more.
        var open = new List<(DateTime ExitUtc, string Symbol)>();
        (DateTime Utc, string Symbol, string Reason)? lastExitAny = null;
        var lastExitSameSide = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var rejectedAt = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        var tradesPerSymbolDay = new Dictionary<(string, DateTime), int>();
        var validEntrySeen = 0;   // for KeepEveryNth

        foreach (var (p, d, _) in timeline)
        foreach (var side in sides)
        {
            var signalType = side == TradeSide.Call ? TradingConst.SignalType.CALL : TradingConst.SignalType.PUT;
            var verdict = _entry.Evaluate(p, side);

            // Rejection = the usual blocked counter plus (optionally) a log row naming what held the signal.
            void Rej(string gate, DateTime? heldUntilUtc = null, string openSymbol = "")
            {
                Block(result, gate);
                if (opt.LogSignals)
                    result.Log.Add(new SignalLogRow(p.TimeIst, p.Symbol, side.ToString().ToUpperInvariant(), verdict.Path, gate,
                        openSymbol, heldUntilUtc + SessionClock.IstOffset, null, "", null, null));
            }
            if (!verdict.SignalProduced)
            {
                if (verdict.BlockedBy?.StartsWith("DataUnavailable", StringComparison.Ordinal) == true)
                    result.DataUnavailable++;
                continue;
            }

            result.TotalSignals++;
            if (!verdict.Candidate)
            {
                Rej(verdict.BlockedBy!);
                continue;
            }

            var symbol = p.Symbol;
            var nowIst = p.TimeIst;

            if (opt.MaxTradesPerSymbolDay is { } cap && tradesPerSymbolDay.GetValueOrDefault((symbol, p.TimeIst.Date)) >= cap)
            {
                Rej("MaxTradesPerSymbolDay");
                continue;
            }

            // OnePositionGate, generalised. Retire anything that has already exited, then apply the cap
            // either across all symbols (production) or per symbol.
            open.RemoveAll(o => o.ExitUtc <= p.TimeUtc);
            var inUse = opt.PerSymbolSlots
                ? open.Count(o => string.Equals(o.Symbol, symbol, StringComparison.OrdinalIgnoreCase))
                : open.Count;
            if (inUse >= opt.MaxConcurrentPositions)
            {
                Rej("OnePositionGate", open.Min(o => o.ExitUtc), string.Join('+', open.Select(o => o.Symbol)));
                continue;
            }

            // OiConfirmationGate (replayable only where option history exists)
            if (_settings.GetBool("EnableOiConfirmationGate", symbol, true) && _options is not null)
            {
                var prevClose = p.NextBarIndex >= 2 ? d.Bars[p.NextBarIndex - 2].Close : p.LastClosedBar.Close;
                var confirms = _options.OiConfirms(symbol, side, nowIst, d.StrikeInterval, p.LastClosedBar.Close - prevClose);
                if (confirms is false)
                {
                    Rej("OiConfirmationGate");
                    continue;
                }
                if (confirms is null)
                    result.OiUnverifiable++;
            }
            else if (_settings.GetBool("EnableOiConfirmationGate", symbol, true))
            {
                result.OiUnverifiable++;
            }

            // DuplicateSignalGate: rejected-entry cooldown, then 5 minutes after a close of the same symbol and direction
            var key = $"{symbol}|{side}";   // DuplicateSignalGate is per symbol AND direction
            var rejectedCooldown = _settings.GetInt("RejectedEntryCooldownMinutes", symbol, 5);
            if (rejectedCooldown > 0 && rejectedAt.TryGetValue(key, out var rej) && p.TimeUtc - rej < TimeSpan.FromMinutes(rejectedCooldown))
            {
                Rej("DuplicateSignalGate", rej + TimeSpan.FromMinutes(rejectedCooldown));
                continue;
            }
            if (lastExitSameSide.TryGetValue(key, out var lastClose) && p.TimeUtc - lastClose < TimeSpan.FromMinutes(5))
            {
                Rej("DuplicateSignalGate", lastClose + TimeSpan.FromMinutes(5));
                continue;
            }

            // HandoffCooldownGate: any exit on any symbol (same direction only when configured)
            if (!opt.DisableHandoffCooldown && _settings.GetBool(HandoffCooldownGate.EnableKey, symbol, false))
            {
                var minutes = _settings.GetInt(HandoffCooldownGate.MinutesKey, symbol, HandoffCooldownGate.DefaultMinutes);
                if (minutes > 0 && lastExitAny is { } le
                    && HandoffCooldownGate.Evaluate(signalType, le.Symbol, le.Utc + SessionClock.IstOffset, le.Reason, nowIst, minutes) is not null)
                {
                    Rej("HandoffCooldownGate", le.Utc + TimeSpan.FromMinutes(minutes));
                    continue;
                }
            }

            // injected rejection (tests): the entry order is rejected, nothing opens, the cooldown starts
            if (opt.RejectEntry?.Invoke(p) == true)
            {
                rejectedAt[key] = p.TimeUtc;
                Rej("OrderRejected");
                continue;
            }

            // Information-free thinning, applied after every real gate so it samples the same
            // population the filters sample from.
            if (opt.KeepEveryNth is { } nth && nth > 1 && validEntrySeen++ % nth != 0)
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
                result.Log.Add(new SignalLogRow(p.TimeIst, symbol, side.ToString().ToUpperInvariant(), verdict.Path, "ENTERED", "", null,
                    trade.ExitUtc + SessionClock.IstOffset, trade.ExitReason, trade.PnlRs, trade.CostRs));
            tradesPerSymbolDay[(symbol, p.TimeIst.Date)] = tradesPerSymbolDay.GetValueOrDefault((symbol, p.TimeIst.Date)) + 1;
            open.Add((trade.ExitUtc, symbol));
            lastExitAny = (trade.ExitUtc, symbol, trade.ExitReason);
            lastExitSameSide[key] = trade.ExitUtc;
        }

        return result;
    }

    private static void Block(SimulationResult r, string gate) =>
        r.BlockedBy[gate] = r.BlockedBy.GetValueOrDefault(gate) + 1;

    private Trade? Execute(MarketPoint p, SymbolData d, SimulationOptions opt, TradeSide side, string entryPath)
    {
        var sign = side == TradeSide.Call ? 1.0 : -1.0;
        var cfg = _exit.Symbols[p.Symbol];

        // the path: following 1m bars of the same session up to (not including) 15:15 IST
        var endUtc = p.TimeUtc.Date + SessionClock.EntryEndUtc;
        var indexMoves = new List<double>();
        var closeTimes = new List<DateTime>();
        for (var q = p.NextBarIndex; q < d.Bars.Length; q++)
        {
            var bar = d.Bars[q];
            if (bar.OpenUtc.Date != p.TimeUtc.Date || bar.OpenUtc >= endUtc)
                break;
            indexMoves.Add(sign * (bar.Close - p.Ltp));
            closeTimes.Add(bar.OpenUtc.AddMinutes(1));
        }
        if (indexMoves.Count == 0)
            return null;

        var source = PremiumSource.IndexDeltaModel;
        var delta = cfg.Delta;
        var entryPremium = cfg.EntryPremium;
        double[] path = indexMoves.Select(m => m * delta).ToArray();

        if (opt.PremiumMode == PremiumMode.OptionHistoryWhereAvailable && _options is not null)
        {
            var real = _options.TryPath(p.Symbol, side, p.TimeIst, closeTimes.Select(c => c + SessionClock.IstOffset).ToList());
            if (real is not null)
            {
                source = PremiumSource.OptionMinuteHistory;
                delta = real.Delta > 0 ? real.Delta : delta;
                entryPremium = real.EntryPremium;
                path = real.Moves;
            }
        }

        var r = _exit.IndexStopPoints(p.Atr) * delta;
        var local = cfg with { Delta = delta, EntryPremium = entryPremium };
        var replay = ExitReplay.Run(path, r, p.Atr, local, _exit, opt.StopModel, side == TradeSide.Call);

        var pnl = replay.PnlPoints * cfg.LotSize;
        double? cost = opt.Costs is { } c ? c.Cost(replay.Orders, cfg.LotSize, _exit.Lots) : null;
        var exitUtc = closeTimes[Math.Min(replay.ExitStep, closeTimes.Count - 1)];

        return new Trade(p.Symbol, side, p.TimeUtc, exitUtc, entryPath, source, entryPremium, r,
            pnl, cost, replay.ExitReason, replay.Orders, replay.ReachedT1, replay.ReachedMid, replay.ReachedFinal,
            Entry: new EntryContext(
                p.Rsi1m, p.Rsi5m, p.Pcr, p.AtrPct, p.OpenGapPct, p.DayChangePct,
                StretchAtr: p.Atr > 0 && p.Ema20 > 0 ? (side == TradeSide.Call ? 1.0 : -1.0) * (p.Ltp - p.Ema20) / p.Atr : 0,
                Efficiency: p.DayHigh - p.DayLow > 0 && p.SessionOpen > 0
                    ? Math.Abs(p.Ltp - p.SessionOpen) / (p.DayHigh - p.DayLow)
                    : null));
    }
}
