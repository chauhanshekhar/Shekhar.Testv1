using System.Globalization;
using System.Text;
using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Simulation;
using Trading.Domain;

namespace Trading.Backtest.Reporting;

/// <summary>One line of the parity report: a real signal and what the replay decided at that minute, or a replay-only entry and what the live audit recorded.</summary>
/// <param name="BacktestPnlRs">Gross P&amp;L (Rs, no costs) of the trade the replay enters at this minute; null when it does not enter.</param>
/// <param name="LivePnlRs">Realised P&amp;L of the real signal: sum of TradeJournal.ProfitLoss for its SignalId; null when none.</param>
public sealed record SignalParityRow(
    string Status, string Symbol, string Side, DateTime MinuteIst, long? LiveSignalId,
    string BacktestBlockedBy, string BacktestReason, string LiveBlockedBy, string LiveReason, string Note,
    double? BacktestPnlRs = null, string BacktestExitReason = "", string PremiumSource = "", decimal? LivePnlRs = null);

/// <summary>
/// Signal parity: does the replay produce the SAME signals as production?
///
///   Direction 1 - every real Signals row: the replay must flag a candidate (passed the chain and every replayed state-free
///                 gate) for that symbol and side at the signal's minute (or the minute before; the row is written after the
///                 order is placed). A miss names the replay gate that blocked it, which is the divergence to fix.
///   Direction 2 - every replay candidate episode (first minute of a consecutive run per symbol and side) with no real signal
///                 within the episode: the live audit (IndicatorSnapshots) for that minute names the live gate that stopped it.
///                 Stateful live gates (OnePositionGate, DuplicateSignalGate, ConsecutiveLossGate, HandoffCooldownGate,
///                 OiConfirmationGate) are expected here; SignalEngine / StrategySelector / BreakoutProtection are not.
/// Read-only: uses rows already loaded by HistoricalDataLoader.
/// </summary>
public static class SignalParityReport
{
    /// <summary>Live gates that depend on positions, orders or the option chain at the second - a replay-only candidate they block is expected.</summary>
    public static readonly HashSet<string> StatefulLiveGates = new(StringComparer.OrdinalIgnoreCase)
    {
        "OnePositionGate", "DuplicateSignalGate", "ConsecutiveLossGate", "HandoffCooldownGate", "OiConfirmationGate",
        "RiskGate", "RiskRewardGate", "SpreadGate", "StaleEntryGate", "StaleDataGate", "KillSwitchGate", "EventBlackoutGate",
        "IndicatorsNotReady", "MaxDailyTradesGate", "CircuitBreakerGate",
    };

    public static List<SignalParityRow> Build(
        IReadOnlyList<SymbolData> data,
        EntryEvaluator evaluator,
        IReadOnlyList<LiveSignalRow> liveSignals,
        IReadOnlyList<LiveAuditMinuteRow> liveAudit,
        DateTime? fromIst = null,
        ExecutionSimulator? simulator = null,
        IReadOnlyDictionary<long, decimal>? livePnl = null)
    {
        // Bars before fromIst were loaded only to warm the indicators up; their decision minutes are not part of the comparison.
        data = From(data, fromIst ?? DateTime.MinValue);

        var rows = new List<SignalParityRow>();
        var pointBy = new Dictionary<(string, DateTime), MarketPoint>();
        foreach (var d in data)
            foreach (var p in d.Points)
                pointBy[(d.Symbol.ToUpperInvariant(), p.TimeIst)] = p;

        var auditBy = new Dictionary<(string, DateTime), LiveAuditMinuteRow>();
        foreach (var a in liveAudit)
            auditBy[(a.Symbol.ToUpperInvariant(), a.MinuteIst)] = a;

        var verdicts = new Dictionary<(string, DateTime, TradeSide), EntryVerdict>();
        EntryVerdict? VerdictAt(string symbol, DateTime minute, TradeSide side)
        {
            var key = (symbol, minute, side);
            if (verdicts.TryGetValue(key, out var v)) return v;
            if (!pointBy.TryGetValue((symbol, minute), out var p)) return null;
            v = evaluator.Evaluate(p, side);
            verdicts[key] = v;
            return v;
        }

        var dataBy = data.ToDictionary(d => d.Symbol.ToUpperInvariant(), d => d);

        // P&L of a replay entry at one minute: the same exit replay the grid uses, without queueing gates.
        SignalParityRow WithPnl(SignalParityRow row, DateTime entryMinute, TradeSide side, string path)
        {
            if (simulator is null || !pointBy.TryGetValue((row.Symbol, entryMinute), out var ep) || !dataBy.TryGetValue(row.Symbol, out var sd))
                return row;
            var trade = simulator.ExecuteAt(ep, sd, side, path);
            return trade is null ? row : row with
            {
                BacktestPnlRs = trade.PnlRs, BacktestExitReason = trade.ExitReason, PremiumSource = trade.Source.ToString(),
            };
        }

        var replayedDates = data.SelectMany(d => d.Points).Select(p => p.TimeIst.Date).ToHashSet();
        var liveMinutes = new HashSet<(string, DateTime, TradeSide)>();

        // ── Direction 1: every real signal ───────────────────────────────────────────────────────────────────────
        foreach (var s in liveSignals)
        {
            var symbol = s.Symbol.ToUpperInvariant();
            var side = string.Equals(s.SignalType, TradingConst.SignalType.PUT, StringComparison.OrdinalIgnoreCase)
                ? TradeSide.Put : TradeSide.Call;
            var sideText = side == TradeSide.Call ? "CALL" : "PUT";
            var minute = Truncate(s.CreatedOn);
            liveMinutes.Add((symbol, minute, side));
            liveMinutes.Add((symbol, minute.AddMinutes(-1), side));

            var note = $"live {s.StrategyType}/{s.SelectedStrategy}/{s.MarketState} status={s.Status} exit={s.ExitReason}";

            if (!replayedDates.Contains(minute.Date))
            {
                rows.Add(new(ParityStatus.NoReplayData, symbol, sideText, minute, s.Id, "", "", "", "", note));
                continue;
            }

            if (string.Equals(s.StrategyType, TradingConst.StrategyType.HeroZero, StringComparison.OrdinalIgnoreCase)
                || s.SelectedStrategy.Contains("HeroZero", StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(new(ParityStatus.NotReplayedHeroZero, symbol, sideText, minute, s.Id, "", "", "", "", note));
                continue;
            }

            if (s.SelectedStrategy.Contains("EarlyCross", StringComparison.OrdinalIgnoreCase))
            {
                rows.Add(new(ParityStatus.NotReplayedEarlyCross, symbol, sideText, minute, s.Id, "", "", "", "", note));
                continue;
            }

            var tod = minute.TimeOfDay;
            if (tod < MarketHours.SignalOpen || tod > MarketHours.SignalClose)
            {
                rows.Add(new(ParityStatus.LiveOutsideSignalWindow, symbol, sideText, minute, s.Id, "", "", "", "",
                    note + " | live fired outside 09:25-15:10 IST (Trading.Worker is built with the TESTING constant, so MarketHours.IsSignalWindow() is always true)"));
                continue;
            }

            var at = VerdictAt(symbol, minute, side);
            var before = VerdictAt(symbol, minute.AddMinutes(-1), side);
            if (at?.Candidate == true || before?.Candidate == true)
            {
                var entryMinute = at?.Candidate == true ? minute : minute.AddMinutes(-1);
                var path = (at?.Candidate == true ? at.Path : before!.Path) ?? "";
                rows.Add(WithPnl(new(ParityStatus.Match, symbol, sideText, minute, s.Id, "", path, "", "", note), entryMinute, side, path));
                continue;
            }

            var blocking = at ?? before;
            rows.Add(blocking is null
                ? new SignalParityRow(ParityStatus.LiveOnlyNoPoint, symbol, sideText, minute, s.Id, "no replay point (indicators not warm / bars missing)", "", "", "", note)
                : new SignalParityRow(ParityStatus.LiveOnly, symbol, sideText, minute, s.Id, blocking.BlockedBy ?? "", blocking.Reason ?? "", "", "", note));
        }

        // ── Direction 2: replay candidate episodes with no real signal ───────────────────────────────────────────────
        foreach (var d in data)
        {
            var symbol = d.Symbol.ToUpperInvariant();
            foreach (var side in new[] { TradeSide.Call, TradeSide.Put })
            {
                var sideText = side == TradeSide.Call ? "CALL" : "PUT";
                DateTime? episodeStart = null;
                var episodeHasLive = false;
                DateTime previous = default;

                void Close()
                {
                    if (episodeStart is { } start && !episodeHasLive)
                    {
                        auditBy.TryGetValue((symbol, start), out var a);
                        var liveBy = a is null ? "(no audit row)" : a.BlockedBy.Length == 0 ? "(nothing blocked)" : a.BlockedBy;
                        var status = a is null ? ParityStatus.BacktestOnlyNoAudit
                            : StatefulLiveGates.Contains(a.BlockedBy) ? ParityStatus.BacktestOnlyStateful
                            : ParityStatus.BacktestOnly;
                        var path = VerdictAt(symbol, start, side)?.Path ?? "";
                        rows.Add(WithPnl(new(status, symbol, sideText, start, null, "", path,
                            liveBy, a?.SampleReason ?? "", a is null ? "" : $"live cycles in minute={a.Cycles} anyGenerated={a.AnyGenerated}"),
                            start, side, path));
                    }
                    episodeStart = null;
                    episodeHasLive = false;
                }

                foreach (var p in d.Points)
                {
                    var v = VerdictAt(symbol, p.TimeIst, side);
                    var candidate = v?.Candidate == true;
                    var contiguous = episodeStart is not null && p.TimeIst - previous <= TimeSpan.FromMinutes(1);
                    if (!candidate || !contiguous)
                        Close();
                    if (candidate)
                    {
                        episodeStart ??= p.TimeIst;
                        if (liveMinutes.Contains((symbol, p.TimeIst, side)))
                            episodeHasLive = true;
                    }
                    previous = p.TimeIst;
                }
                Close();
            }
        }

        if (livePnl is not null)
            rows = rows.Select(r => r.LiveSignalId is { } id && livePnl.TryGetValue(id, out var pnl) ? r with { LivePnlRs = pnl } : r).ToList();

        return rows.OrderBy(r => r.MinuteIst).ThenBy(r => r.Symbol).ThenBy(r => r.Side).ToList();
    }

    /// <summary>The same series with decision minutes before <paramref name="fromIst"/> removed (bars kept for the exit replay).</summary>
    public static List<SymbolData> From(IReadOnlyList<SymbolData> data, DateTime fromIst) =>
        data.Select(d => new SymbolData
        {
            Symbol = d.Symbol, Bars = d.Bars, StrikeInterval = d.StrikeInterval,
            Points = d.Points.Where(p => p.TimeIst >= fromIst).ToArray(),
        }).ToList();

    /// <summary>
    /// backtest-trades.csv: ONLY the trades the replay takes, in time order, with totals per symbol and overall. No real
    /// signals are listed; LiveSignalId is filled when a real signal fired for the same symbol and side at that minute
    /// (or the minute before), so the two can be cross-checked.
    /// </summary>
    public static void WriteBacktestTrades(string outDir, IReadOnlyList<Trade> trades, IReadOnlyList<LiveSignalRow> liveSignals)
    {
        static string Rs(double v) => v.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture);

        var liveBy = new Dictionary<(string, string, DateTime), long>();
        foreach (var s in liveSignals)
        {
            var minute = Truncate(s.CreatedOn);
            var key = (s.Symbol.ToUpperInvariant(), s.SignalType.ToUpperInvariant());
            liveBy.TryAdd((key.Item1, key.Item2, minute), s.Id);
            liveBy.TryAdd((key.Item1, key.Item2, minute.AddMinutes(1)), s.Id);
        }

        var lines = new List<string> { "Date,EntryIst,ExitIst,Symbol,Side,EntryPath,ExitReason,PremiumSource,EntryPremium,PnlRs,CostRs,NetRs,Result,LiveSignalId" };
        foreach (var t in trades.OrderBy(t => t.EntryUtc))
        {
            var entryIst = t.EntryUtc + SessionClock.IstOffset;
            var side = t.Side == TradeSide.Call ? "CALL" : "PUT";
            liveBy.TryGetValue((t.Symbol.ToUpperInvariant(), side, entryIst), out var liveId);
            lines.Add($"{entryIst:yyyy-MM-dd},{entryIst:HH:mm},{t.ExitUtc + SessionClock.IstOffset:HH:mm},{t.Symbol},{side}," +
                      $"{t.EntryPath},{t.ExitReason},{t.Source},{t.EntryPremium.ToString("0.##", CultureInfo.InvariantCulture)}," +
                      $"{Rs(t.PnlRs)},{Rs(t.CostRs ?? 0)},{Rs(t.PnlRs - (t.CostRs ?? 0))},{(t.PnlRs > 0 ? "WIN" : "LOSS")},{(liveId > 0 ? liveId.ToString(CultureInfo.InvariantCulture) : "")}");
        }

        string Footer(string label, IReadOnlyList<Trade> set) =>
            $"{label},,,,,{set.Count} trades,{set.Count(x => x.PnlRs > 0)} win / {set.Count(x => x.PnlRs <= 0)} loss,,," +
            $"{Rs(set.Sum(x => x.PnlRs))},{Rs(set.Sum(x => x.CostRs ?? 0))},{Rs(set.Sum(x => x.PnlRs - (x.CostRs ?? 0)))},,";

        lines.Add("");
        foreach (var g in trades.GroupBy(t => t.TradeDate).OrderBy(g => g.Key))
            lines.Add(Footer($"TOTAL {g.Key:yyyy-MM-dd}", g.ToList()));
        foreach (var g in trades.GroupBy(t => t.Symbol).OrderBy(g => g.Key))
            lines.Add(Footer($"TOTAL {g.Key}", g.ToList()));
        lines.Add(Footer("TOTAL", trades));

        SafeWrite(Path.Combine(outDir, "backtest-trades.csv"), path => File.WriteAllLines(path, lines));

        // Same rows as a coloured workbook: blue header, green/red P&L per trade, yellow total rows.
        var sheet = new List<IReadOnlyList<XlsxCell>>
        {
            new[] { "Date", "EntryIst", "ExitIst", "Symbol", "Side", "EntryPath", "ExitReason", "PremiumSource", "EntryPremium", "PnlRs", "CostRs", "NetRs", "Result", "LiveSignalId" }
                .Select(h => new XlsxCell(h, XlsxStyle.Header)).ToList(),
        };
        var ordered = trades.OrderBy(t => t.EntryUtc).ToList();
        foreach (var t in ordered)
        {
            var entryIst = t.EntryUtc + SessionClock.IstOffset;
            var side = t.Side == TradeSide.Call ? "CALL" : "PUT";
            liveBy.TryGetValue((t.Symbol.ToUpperInvariant(), side, entryIst), out var liveId);
            var win = t.PnlRs > 0;
            var net = Math.Round(t.PnlRs - (t.CostRs ?? 0), 2);
            sheet.Add(new XlsxCell[]
            {
                new($"{entryIst:yyyy-MM-dd}"), new($"{entryIst:HH:mm}"), new($"{t.ExitUtc + SessionClock.IstOffset:HH:mm}"),
                new(t.Symbol), new(side), new(t.EntryPath), new(t.ExitReason), new(t.Source.ToString()),
                new(Math.Round(t.EntryPremium, 2), XlsxStyle.Decimal),
                new(Math.Round(t.PnlRs, 2), win ? XlsxStyle.Win : XlsxStyle.Loss),
                new(Math.Round(t.CostRs ?? 0, 2), XlsxStyle.Decimal),
                new(net, net > 0 ? XlsxStyle.Win : XlsxStyle.Loss),
                new(win ? "WIN" : "LOSS", win ? XlsxStyle.Win : XlsxStyle.Loss),
                new(liveId > 0 ? (object)liveId : null),
            });
        }

        IReadOnlyList<XlsxCell> FooterRow(string label, IReadOnlyList<Trade> set)
        {
            var pnl = Math.Round(set.Sum(x => x.PnlRs), 2);
            var cells = Enumerable.Repeat(new XlsxCell(null, XlsxStyle.FooterText), 14).ToArray();
            cells[0] = new(label, XlsxStyle.FooterText);
            cells[4] = new($"{set.Count} trades", XlsxStyle.FooterText);
            cells[5] = new($"{set.Count(x => x.PnlRs > 0)} win / {set.Count(x => x.PnlRs <= 0)} loss", XlsxStyle.FooterText);
            cells[9] = new(pnl, pnl > 0 ? XlsxStyle.FooterWin : pnl < 0 ? XlsxStyle.FooterLoss : XlsxStyle.FooterNumber);
            var cost = Math.Round(set.Sum(x => x.CostRs ?? 0), 2);
            var netTotal = Math.Round(pnl - cost, 2);
            cells[10] = new(cost, XlsxStyle.FooterNumber);
            cells[11] = new(netTotal, netTotal > 0 ? XlsxStyle.FooterWin : netTotal < 0 ? XlsxStyle.FooterLoss : XlsxStyle.FooterNumber);
            return cells;
        }

        sheet.Add(Array.Empty<XlsxCell>());
        foreach (var g in ordered.GroupBy(t => t.TradeDate).OrderBy(g => g.Key))
            sheet.Add(FooterRow($"TOTAL {g.Key:yyyy-MM-dd}", g.ToList()));
        foreach (var g in ordered.GroupBy(t => t.Symbol).OrderBy(g => g.Key))
            sheet.Add(FooterRow($"TOTAL {g.Key}", g.ToList()));
        sheet.Add(FooterRow("TOTAL", ordered));

        SafeWrite(Path.Combine(outDir, "backtest-trades.xlsx"), path =>
            XlsxWriter.Write(path, "Backtest trades", sheet,
                [12, 9, 9, 12, 7, 16, 22, 20, 13, 13, 11, 13, 8, 13], dataRowCount: ordered.Count));
    }

    public static void Write(string outDir, IReadOnlyList<SignalParityRow> rows, string header)
    {
        static string Csv(string? v) => "\"" + (v ?? "").Replace("\"", "\"\"") + "\"";

        static string Rs(double? v) => v is { } x ? x.ToString("+0.00;-0.00;0.00", CultureInfo.InvariantCulture) : "";

        var csv = new List<string>
        {
            "Status,Symbol,Side,MinuteIst,LiveSignalId,BacktestBlockedBy,BacktestReasonOrPath,LiveBlockedBy,LiveReason,Note," +
            "BacktestPnlRs,BacktestExitReason,PremiumSource,LivePnlRs",
        };
        csv.AddRange(rows.Select(r =>
            $"{r.Status},{r.Symbol},{r.Side},{r.MinuteIst:yyyy-MM-dd HH:mm},{r.LiveSignalId}," +
            $"{Csv(r.BacktestBlockedBy)},{Csv(r.BacktestReason)},{Csv(r.LiveBlockedBy)},{Csv(r.LiveReason)},{Csv(r.Note)}," +
            $"{Rs(r.BacktestPnlRs)},{Csv(r.BacktestExitReason)},{r.PremiumSource},{Rs((double?)r.LivePnlRs)}"));

        // Footer: totals. TOTAL_BACKTEST counts every replay entry; TOTAL_BACKTEST_EXCL_STATEFUL leaves out entries that a
        // live position/cooldown gate blocked (the replay would mostly have been blocked by the same queue); TOTAL_MATCH is the
        // trades both sides took; TOTAL_LIVE is the realised P&L of the real signals.
        csv.Add("");
        foreach (var (label, set, live) in Totals(rows))
            csv.Add($"{label},,,,,{Csv(set)},,,,,{Rs(live.Backtest)},,,{Rs(live.Live)}");
        SafeWrite(Path.Combine(outDir, "signal-parity.csv"), path => File.WriteAllLines(path, csv));
        SafeWrite(Path.Combine(outDir, "signal-parity-summary.md"), path => File.WriteAllText(path, Summary(rows, header)));
    }

    /// <summary>
    /// Writes the file; when it is locked (typically still open in Excel) writes "name-HHmmss.ext" next to it instead of
    /// throwing away the run.
    /// </summary>
    private static void SafeWrite(string path, Action<string> write)
    {
        try
        {
            write(path);
        }
        catch (IOException)
        {
            var alt = Path.Combine(Path.GetDirectoryName(path) ?? "",
                $"{Path.GetFileNameWithoutExtension(path)}-{DateTime.Now:HHmmss}{Path.GetExtension(path)}");
            write(alt);
            Console.WriteLine($"{Path.GetFileName(path)} is open in another program (Excel?) - wrote {alt} instead.");
        }
    }

    /// <summary>Footer lines: (label, description with counts and win/loss, (backtest Rs, live Rs)).</summary>
    public static IEnumerable<(string Label, string Set, (double? Backtest, double? Live) Totals)> Totals(IReadOnlyList<SignalParityRow> rows)
    {
        static string WinLoss(IEnumerable<double> pnl)
        {
            var list = pnl.ToList();
            return $"{list.Count} trades, {list.Count(x => x > 0)} win / {list.Count(x => x <= 0)} loss";
        }

        var bt = rows.Where(r => r.BacktestPnlRs is not null).ToList();
        var btClean = bt.Where(r => r.Status != ParityStatus.BacktestOnlyStateful).ToList();
        var match = bt.Where(r => r.Status == ParityStatus.Match).ToList();
        var live = rows.Where(r => r.LivePnlRs is not null).ToList();

        yield return ("TOTAL_BACKTEST", "all replay entries: " + WinLoss(bt.Select(r => r.BacktestPnlRs!.Value)),
            (bt.Sum(r => r.BacktestPnlRs!.Value), null));
        yield return ("TOTAL_BACKTEST_EXCL_STATEFUL", "without entries a live position/cooldown blocked: " + WinLoss(btClean.Select(r => r.BacktestPnlRs!.Value)),
            (btClean.Sum(r => r.BacktestPnlRs!.Value), null));
        yield return ("TOTAL_MATCH", "signals both took (backtest vs live): " + WinLoss(match.Select(r => r.BacktestPnlRs!.Value)),
            (match.Sum(r => r.BacktestPnlRs!.Value), match.Where(r => r.LivePnlRs is not null).Sum(r => (double)r.LivePnlRs!.Value)));
        yield return ("TOTAL_LIVE", "real signals (TradeJournal): " + WinLoss(live.Select(r => (double)r.LivePnlRs!.Value)),
            (null, live.Sum(r => (double)r.LivePnlRs!.Value)));
    }

    public static string Summary(IReadOnlyList<SignalParityRow> rows, string header)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Signal parity: replay vs real signals").AppendLine().AppendLine(header).AppendLine();

        sb.AppendLine("## Profit (Rs; backtest is gross, no brokerage/slippage)").AppendLine()
          .AppendLine("| Total | Trades | Backtest P&L | Live P&L |").AppendLine("|---|---|---:|---:|");
        foreach (var (label, set, t) in Totals(rows))
            sb.AppendLine($"| {label} | {set} | {(t.Backtest is { } b ? b.ToString("+#,0.00;-#,0.00;0.00", CultureInfo.InvariantCulture) : "")} | " +
                          $"{(t.Live is { } l ? l.ToString("+#,0.00;-#,0.00;0.00", CultureInfo.InvariantCulture) : "")} |");
        sb.AppendLine();

        var live = rows.Where(r => r.LiveSignalId is not null).ToList();
        var comparable = live.Where(r => r.Status is ParityStatus.Match or ParityStatus.LiveOnly or ParityStatus.LiveOnlyNoPoint).ToList();
        var matched = comparable.Count(r => r.Status == ParityStatus.Match);
        sb.AppendLine($"Real signals: {live.Count}; comparable (Directional, inside the window, with replay data): {comparable.Count}; " +
                      $"reproduced by the replay: {matched} ({(comparable.Count == 0 ? 0 : 100.0 * matched / comparable.Count):F1}%).").AppendLine();

        sb.AppendLine("## Every status").AppendLine().AppendLine("| Status | Count |").AppendLine("|---|---:|");
        foreach (var g in rows.GroupBy(r => r.Status).OrderByDescending(g => g.Count()))
            sb.AppendLine($"| {g.Key} | {g.Count()} |");

        sb.AppendLine().AppendLine("## Real signals the replay missed, by the replay gate that blocked them (fix these first)").AppendLine()
          .AppendLine("| Replay BlockedBy | Count |").AppendLine("|---|---:|");
        foreach (var g in live.Where(r => r.Status == ParityStatus.LiveOnly).GroupBy(r => r.BacktestBlockedBy).OrderByDescending(g => g.Count()))
            sb.AppendLine($"| {g.Key} | {g.Count()} |");

        sb.AppendLine().AppendLine("## Replay-only entries, by the live gate that stopped them").AppendLine()
          .AppendLine("| Live BlockedBy | Stateful (expected) | Count |").AppendLine("|---|---|---:|");
        foreach (var g in rows.Where(r => r.LiveSignalId is null).GroupBy(r => r.LiveBlockedBy).OrderByDescending(g => g.Count()))
            sb.AppendLine($"| {g.Key} | {(StatefulLiveGates.Contains(g.Key) ? "yes" : "no")} | {g.Count()} |");

        sb.AppendLine().AppendLine("## Per day").AppendLine()
          .AppendLine("| Date | Real | Reproduced | Missed | Replay-only (non-stateful) |").AppendLine("|---|---:|---:|---:|---:|");
        foreach (var g in rows.GroupBy(r => r.MinuteIst.Date).OrderBy(g => g.Key))
            sb.AppendLine($"| {g.Key:yyyy-MM-dd} | {g.Count(r => r.LiveSignalId is not null)} | {g.Count(r => r.Status == ParityStatus.Match)} | " +
                          $"{g.Count(r => r.Status is ParityStatus.LiveOnly or ParityStatus.LiveOnlyNoPoint)} | {g.Count(r => r.Status == ParityStatus.BacktestOnly)} |");

        return sb.ToString();
    }

    private static DateTime Truncate(DateTime t) => new(t.Year, t.Month, t.Day, t.Hour, t.Minute, 0, t.Kind);
}

public static class ParityStatus
{
    public const string Match = "MATCH";
    public const string LiveOnly = "LIVE_ONLY";
    public const string LiveOnlyNoPoint = "LIVE_ONLY_NO_POINT";
    public const string BacktestOnly = "BACKTEST_ONLY";
    public const string BacktestOnlyStateful = "BACKTEST_ONLY_STATEFUL";
    public const string BacktestOnlyNoAudit = "BACKTEST_ONLY_NO_AUDIT";
    public const string LiveOutsideSignalWindow = "LIVE_OUTSIDE_WINDOW";
    public const string NotReplayedHeroZero = "NOT_REPLAYED_HEROZERO";
    public const string NotReplayedEarlyCross = "NOT_REPLAYED_EARLYCROSS";
    public const string NoReplayData = "NO_REPLAY_DATA";
}
