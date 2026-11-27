
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.Net.Sockets;
using Trading.Backtest.Configurations;
using Trading.Backtest.Data;
using Trading.Backtest.Discovery;
using Trading.Backtest.Entry;
using Trading.Backtest.Indicators;
using Trading.Backtest.Reporting;
using Trading.Backtest.Settings;
using Trading.Backtest.Simulation;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Backtest;

internal static class Program
{
    // Rs20 per order + 1 point slippage per side (the real figures, confirmed 2026-10-15); override with --cost-per-order / --slippage-points.
    private static CostModel RunCosts(Args o) => new(
        double.Parse(o.Get("cost-per-order", "20"), CultureInfo.InvariantCulture),
        double.Parse(o.Get("slippage-points", "1"), CultureInfo.InvariantCulture));

    private static async Task<int> Main(string[] args)
    {
        var o = Args.Parse(args);

        var side = o.Get("side", "BOTH").ToUpperInvariant();

        var sides = side switch
        {
            "CALL" => new[] { TradeSide.Call },
            "PUT" => new[] { TradeSide.Put },
            _ => new[] { TradeSide.Call, TradeSide.Put }
        };

        var grid = new GridOptions
        {
            MaxGridConfigurations = int.Parse(o.Get("max-grid", "600")),
            CallRsiFloors = o.Has("call-rsi-floors")
                ? Decimals(o.Get("call-rsi-floors", ""))
                : new GridOptions().CallRsiFloors,
            CallRsiCeilings = o.Has("call-rsi-ceilings")
                ? Decimals(o.Get("call-rsi-ceilings", ""))
                : new GridOptions().CallRsiCeilings,
            PutRsiFloors = o.Has("put-rsi-floors")
                ? Decimals(o.Get("put-rsi-floors", ""))
                : new GridOptions().PutRsiFloors,
            PutRsiCeilings = o.Has("put-rsi-ceilings")
                ? Decimals(o.Get("put-rsi-ceilings", ""))
                : new GridOptions().PutRsiCeilings,
            EfficiencyThresholds = o.Has("efficiency")
                ? Decimals(o.Get("efficiency", ""))
                : new GridOptions().EfficiencyThresholds,
            StretchMaxAtr = o.Has("stretch-max")
                ? Decimals(o.Get("stretch-max", ""))
                : new GridOptions().StretchMaxAtr,
            StretchMinAtr = o.Has("stretch-min")
                ? Decimals(o.Get("stretch-min", ""))
                : new GridOptions().StretchMinAtr,
        };

        if (o.Has("list-configs"))
        {
            foreach (var s in sides)
                foreach (var c in SelectConfigurations(o, s, grid))
                    Console.WriteLine($"{c.Name}\t{c.Description}");

            return 0;
        }

        var connection = ResolveConnection(o);

        if (connection is null)
        {
            Console.Error.WriteLine(
                "No connection string: pass --connection, set TRADING_BACKTEST_CONNECTION, or provide the Worker appsettings.");

            return 2;
        }

        var from = DateTime.Parse(
            o.Get("from", "2026-07-10"),
            null,
            System.Globalization.DateTimeStyles.AssumeUniversal |
            System.Globalization.DateTimeStyles.AdjustToUniversal);

        // --backtest-trades without --from: the whole history, from the first stored 1m candle of any index.
        if (o.Has("backtest-trades") && !o.Has("from"))
        {
            var first = await new HistoricalDataLoader(connection)
                .LoadFirstCandleDateAsync(ExitModel.SymbolNames.Select(index => $"{index}_1m").ToList());
            if (first is { } firstUtc)
                from = DateTime.SpecifyKind(firstUtc.Date, DateTimeKind.Utc);
            Console.WriteLine($"Backtest over ALL history: from {from:yyyy-MM-dd} (first stored candle) to the last stored candle.");
        }

        var outDir = o.Get(
            "out",
            Path.Combine("docs", "research", $"{DateTime.Now:yyyy-MM-dd}-backtest-grid"));

        Directory.CreateDirectory(outDir);

        var loader = new HistoricalDataLoader(connection);

        // Independent discovery mode.
        if (o.Has("discover"))
        {
            var fromIst = DateTime.SpecifyKind(
                from + SessionClock.IstOffset,
                DateTimeKind.Unspecified);

            var rawSnapshots =
                await loader.LoadDiscoverySnapshotsAsync(fromIst);

            var barsBySymbol = new Dictionary<string, Bar[]>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var symbol in ExitModel.SymbolNames)
            {
                var bars = await loader.LoadBarsAsync($"{symbol}_1m", from);

                if (bars.Count > 0)
                    barsBySymbol[symbol] = bars.OrderBy(b => b.OpenUtc).ToArray();
            }

            var discoveryOptions = new DiscoveryOptions(
                ForwardMinutes: int.Parse(o.Get("discover-forward", "15")),
                TargetAtr: decimal.Parse(
                    o.Get("discover-target-atr", "1.0"),
                    System.Globalization.CultureInfo.InvariantCulture),
                StopAtr: decimal.Parse(
                    o.Get("discover-stop-atr", "0.7"),
                    System.Globalization.CultureInfo.InvariantCulture),
                MinimumTrades: int.Parse(o.Get("discover-min-trades", "20")));

            var rules = DiscoveryRuleFactory.Build();

            var discovered =
                new HistoricalSignalDiscovery(discoveryOptions)
                    .Run(rawSnapshots, barsBySymbol, rules);

            DiscoveryReportWriter.Write(
                outDir,
                discovered,
                discoveryOptions.MinimumTrades);

            Console.WriteLine(
                $"Discovery complete: {rawSnapshots.Count} raw snapshots, {rules.Count} fresh rules, {discovered.Count} simulated candidates.");

            Console.WriteLine(
                $"Reports: {Path.Combine(outDir, "discovery-ranking.csv")}, discovery-candidates.csv");

            Console.WriteLine(
                "Note: this version evaluates underlying index ATR outcomes, not option-premium P&L.");

            return 0;
        }

        var live = await loader.LoadSettingsAsync();

        // Settings as they were on the replayed days: --set "CallRsiCeiling=85,PutRsiFloor=28@SENSEX" (KEY=VALUE[@SYMBOL]).
        // StrategySettings keeps only the current value, so a day replayed after a change needs its old value supplied here.
        List<(string Key, string? Symbol, decimal Value)> overrides = o.Has("set") ? ParseOverrides(o.Get("set", "")) : [];
        foreach (var (key, symbol, value) in overrides)
        {
            live.Set(key, symbol, value);
            Console.WriteLine($"Setting override: {key}{(symbol is null ? "" : "@" + symbol)} = {value}");
        }

        // BreakoutProtection follows StrategySettings.EnableBreakoutProtection exactly like live (no row = off).
        // --breakout off forces it off for a research run whatever the database says.
        var skipBreakout = o.Get("breakout", "db").Equals("off", StringComparison.OrdinalIgnoreCase);
        Console.WriteLine(skipBreakout
            ? "BreakoutProtection: forced OFF by --breakout off"
            : $"BreakoutProtection: from StrategySettings.EnableBreakoutProtection " +
              $"(NIFTY={live.GetBool("EnableBreakoutProtection", "NIFTY", false)}, " +
              $"BANKNIFTY={live.GetBool("EnableBreakoutProtection", "BANKNIFTY", false)}, " +
              $"SENSEX={live.GetBool("EnableBreakoutProtection", "SENSEX", false)})");
        var instruments = await loader.LoadInstrumentsAsync();
        var exitInputs = await loader.LoadExitInputsAsync(instruments);

        // P&L is points x lot size: show what each index is priced with, and refuse to price an index with no lot size.
        foreach (var sym in ExitModel.SymbolNames)
        {
            var active = instruments.Any(i => string.Equals(i.Symbol, sym, StringComparison.OrdinalIgnoreCase));
            if (exitInputs.LotSizes.TryGetValue(sym, out var lot) && lot > 0)
                Console.WriteLine($"{sym}: lot size {lot}{(active ? "" : " (instrument is INACTIVE in TradingInstruments today)")}");
            else
                Console.WriteLine($"PARITY WARNING: {sym}: no LotSize in TradingInstruments - its P&L would be per 1 unit. Fix the TradingInstruments row.");
        }
        var trading = LoadTradingSettings();

        // Research only: --t1-mult 0.75 replays with AtrTarget1Multiplier = 0.75 (T1 = 0.75 / AtrStopLossMultiplier R).
        // Without it the appsettings value is used, exactly as before.
        if (o.Has("t1-mult"))
        {
            trading.AtrTarget1Multiplier = decimal.Parse(o.Get("t1-mult", ""), System.Globalization.CultureInfo.InvariantCulture);
            Console.WriteLine(
                $"T1 override: AtrTarget1Multiplier = {trading.AtrTarget1Multiplier} " +
                $"(= {trading.AtrTarget1Multiplier / trading.AtrStopLossMultiplier:0.00}R at stop multiplier {trading.AtrStopLossMultiplier})");
        }

        // Live trading mode decides the entry trigger candle (Paper: last closed 5m bar, Live: last closed 1m bar,
        // UseOneMinuteEntryCandle overrides). --trading-mode wins over the database row.
        TradingMode? tradingMode = o.Has("trading-mode")
            ? Enum.Parse<TradingMode>(o.Get("trading-mode", "Paper"), ignoreCase: true)
            : await loader.LoadTradingModeAsync();
        Console.WriteLine(
            $"Trading mode replayed for the trigger candle: {tradingMode?.ToString() ?? "unknown (1m bar)"}" +
            (o.Has("trading-mode") ? " (from --trading-mode)" : " (from SystemSettings.TradingMode)"));

        foreach (var warning in ParityWarnings(live, o.Has("regime-replay") || o.Has("signal-parity") || o.Has("backtest-trades")))
            Console.WriteLine($"PARITY WARNING: {warning}");

        Console.WriteLine(
            "Loading candles, snapshots and option history (read-only)...");

        var snapshots = await loader.LoadSnapshotsAsync(from);
        var optionRows = await loader.LoadOptionHistoryAsync();

        var options = optionRows.Count > 0
            ? new OptionHistoryIndex(optionRows)
            : null;

        // Real entry quotes (Signals / IndicatorSnapshots) for the days OptionMinuteHistory does not cover.
        var quoteRows = await loader.LoadEntryQuotesAsync(from + SessionClock.IstOffset);
        var quotes = quoteRows.Count > 0 ? new EntryQuoteIndex(quoteRows) : null;
        Console.WriteLine(
            $"Entry quotes from DB (for days without option minute history): {quoteRows.Count(q => q.Origin == "Signals")} from Signals, " +
            $"{quoteRows.Count(q => q.Origin == "IndicatorSnapshots")} from IndicatorSnapshots");

        var data = new List<SymbolData>();
        var bars5BySymbol = new Dictionary<string, Bar[]>();

        foreach (var symbol in ExitModel.SymbolNames)
        {
            var b1 = await loader.LoadBarsAsync($"{symbol}_1m", from);
            var b5 = await loader.LoadBarsAsync($"{symbol}_5m", from);
            Console.WriteLine(
    $"1m: Count={b1.Count}, First={b1.FirstOrDefault().OpenUtc:O}, Last={b1.LastOrDefault().OpenUtc:O}");

            Console.WriteLine(
                $"5m: Count={b5.Count}, First={b5.FirstOrDefault().OpenUtc:O}, Last={b5.LastOrDefault().OpenUtc:O}");
            if (b1.Count == 0)
            {
                Console.WriteLine($"  {symbol}: no 1m bars, skipped");
                continue;
            }

            var points = MarketSeriesBuilder.Build(
                symbol,
                b1,
                b5,
                o.Has("gap-neutral-rsi5"));
            Console.WriteLine(
    $"Points: Count={points.Count}, First={points.FirstOrDefault()?.TimeIst:yyyy-MM-dd HH:mm:ss}");

            points = MarketSeriesBuilder.AttachSnapshots(
                points,
                snapshots.Where(s => s.Symbol == symbol).ToList(),
                TimeSpan.FromMinutes(3));

            if (o.Has("regime-replay") || o.Has("signal-parity") || o.Has("backtest-trades"))
            {
                points = RegimeEnricher.Attach(
                    symbol,
                    points,
                    b1.OrderBy(b => b.OpenUtc).ToArray(),
                    trading);
            }

            var interval = instruments
                .FirstOrDefault(i =>
                    string.Equals(
                        i.Symbol,
                        symbol,
                        StringComparison.OrdinalIgnoreCase))
                ?.StrikeInterval ?? 50;

            data.Add(new SymbolData
            {
                Symbol = symbol,
                Points = points.ToArray(),
                Bars = b1.OrderBy(b => b.OpenUtc).ToArray(),
                StrikeInterval = interval
            });

            bars5BySymbol[symbol] = b5.OrderBy(b => b.OpenUtc).ToArray();

            Console.WriteLine(
                $"  {symbol}: {b1.Count} 1m bars, {b5.Count} 5m bars, {points.Count} decision minutes, " +
                $"{points.Select(p => p.TimeIst.Date).Distinct().Count()} sessions, snapshot join {points.Count(p => p.Pcr is not null)} rows");
        }

        if (data.Count == 0)
        {
            Console.Error.WriteLine("No candle data.");
            return 3;
        }

        if (o.Has("parity"))
        {
            var parityFrom = DateTime.Parse(o.Get("parity-from", "2026-10-02"));

            var snaps = await ParityCheck.LoadAsync(
                loader.Options,
                parityFrom);

            Console.WriteLine(
                $"Parity: rebuilt indicators vs stored snapshots (last second of each minute, sessions from {parityFrom:yyyy-MM-dd}, {snaps.Count} snapshots)");

            foreach (var d in data)
                foreach (var line in ParityCheck.Compare(d.Symbol, d.Points, snaps))
                    Console.WriteLine(line);

            foreach (var d in data)
            {
                var withBoth = d.Points
                    .Where(p =>
                        p.ReplayedMarketState is not null &&
                        !string.IsNullOrEmpty(p.MarketState) &&
                        p.TimeIst >= parityFrom)
                    .ToList();

                if (withBoth.Count == 0)
                    continue;

                var stateMatch = withBoth.Count(p =>
                    string.Equals(
                        p.ReplayedMarketState,
                        p.MarketState,
                        StringComparison.OrdinalIgnoreCase));

                var regimeMatch = withBoth.Count(p =>
                    p.StoredMarketRegime is not null &&
                    p.StoredMarketRegime.StartsWith(
                        p.RegimeTrend + "/" + p.RegimeVolatility,
                        StringComparison.OrdinalIgnoreCase));

                foreach (var g in withBoth
                    .Where(p =>
                        !string.Equals(
                            p.ReplayedMarketState,
                            p.MarketState,
                            StringComparison.OrdinalIgnoreCase))
                    .GroupBy(p =>
                        $"stored '{(string.IsNullOrEmpty(p.MarketState) ? "(empty)" : p.MarketState)}' -> rebuilt '{p.ReplayedMarketState}'")
                    .OrderByDescending(g => g.Count())
                    .Take(4))
                {
                    Console.WriteLine(
                        $"      {d.Symbol,-10} differs: {g.Key} x{g.Count()}");
                }

                Console.WriteLine(
                    $"  {d.Symbol,-10} MarketState rebuilt = stored on {100.0 * stateMatch / withBoth.Count:F1}%   MarketRegime rebuilt = stored on {100.0 * regimeMatch / withBoth.Count:F1}%   (n={withBoth.Count})");
            }

            return 0;
        }

        // --backtest-trades: ONLY the replay's own trades (backtest-trades.xlsx/.csv), simulated like live, no comparison files.
        if (o.Has("backtest-trades"))
        {
            var fromIst = from + SessionClock.IstOffset;
            var evaluator = new EntryEvaluator(live, trading, instruments, SelectorMode.Replay,
                new EntryRules { SkipBreakoutProtection = skipBreakout }, tradingMode);
            var exit = ExitModel.From(live, trading, exitInputs.AvgDelta, exitInputs.AvgPremium, exitInputs.LotSizes);
            // Research only: --fixed-bracket 30 = stop 30 and target 30 premium points from entry, full exit, no live ladder.
            double? fixedBracket = o.Has("fixed-bracket")
                ? double.Parse(o.Get("fixed-bracket", ""), System.Globalization.CultureInfo.InvariantCulture)
                : null;
            // --fixed-target 60 keeps the stop at --fixed-bracket points but moves the target to 60.
            double? fixedTarget = o.Has("fixed-target")
                ? double.Parse(o.Get("fixed-target", ""), System.Globalization.CultureInfo.InvariantCulture)
                : null;
            if (fixedBracket is { } fb)
                Console.WriteLine($"FIXED BRACKET: stop = entry - {fb}, target = entry + {fixedTarget ?? fb} premium points, full exit (live ladder NOT used).");

            var simulator = new ExecutionSimulator(evaluator, live, exit, options, quotes)
            {
                CrossIndexSymbols = instruments.Select(i => i.Symbol).ToList(),
                FixedBracketPoints = fixedBracket,
                FixedTargetPoints = fixedTarget,
            };

            // Research only: --only-symbols SENSEX,BANKNIFTY keeps every symbol's bars (cross-index gate) but only trades these.
            var onlySymbols = o.Has("only-symbols")
                ? o.Get("only-symbols", "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                : null;
            if (onlySymbols is not null)
                Console.WriteLine($"Trading only: {string.Join(", ", onlySymbols)}");
            foreach (var sym in ExitModel.SymbolNames)
            {
                var ec = exit.Symbols[sym];
                Console.WriteLine(
                    $"{sym}: lots {ec.Lots ?? exit.Lots}, lot size {ec.LotSize}, breakeven arm {ec.ArmOn} (buffer {ec.Buffer}), " +
                    $"half-sale at arm {ec.PartialSaleAtArm}, ladder {ec.Ladder}, step-lock {ec.StepLock}, T2 partial {ec.T2Partial}");
            }

            // One open position across every symbol and both sides (OnePositionGate), same-side cooldown after a stop,
            // live stop ladder, real option prices where stored. Warm-up days before --from are not traded.
            var tradable = SignalParityReport.From(data, fromIst);
            if (onlySymbols is not null)
                tradable = tradable.Select(d => onlySymbols.Contains(d.Symbol)
                    ? d
                    : new SymbolData { Symbol = d.Symbol, Bars = d.Bars, StrikeInterval = d.StrikeInterval, Points = [] }).ToList();

            var run = simulator.Run(
                tradable,
                new SimulationOptions(TradeSide.Call, StopModel.Live, PremiumMode.OptionHistoryWhereAvailable, RunCosts(o), BothSides: true,
                    DayRisk: DayRiskLimits.From(trading, tradingMode),
                    StopFillAtClose: o.Get("stop-fill", "") == "close",
                    StopTouchLow: o.Get("stop-touch", "") == "low"));
            SignalParityReport.WriteBacktestTrades(outDir, run.Trades, []);

            Console.WriteLine(
                $"Backtest {fromIst:yyyy-MM-dd} onward, current settings" +
                (overrides.Count == 0 ? "" : " + overrides") + $", BreakoutProtection {(skipBreakout ? "forced OFF" : "from StrategySettings")}: " +
                $"{run.Trades.Count} trades ({run.Trades.Count(t => t.PnlRs > 0)} win / {run.Trades.Count(t => t.PnlRs <= 0)} loss), " +
                $"gross P&L {run.Trades.Sum(t => t.PnlRs):+0.00;-0.00;0.00} Rs, " +
                $"costs {run.Trades.Sum(t => t.CostRs ?? 0):0.00} Rs, NET {run.Trades.Sum(t => t.PnlRs - (t.CostRs ?? 0)):+0.00;-0.00;0.00} Rs");
            Console.WriteLine($"Wrote {Path.Combine(outDir, "backtest-trades.xlsx")} and backtest-trades.csv");
            return 0;
        }

        // Signal parity: does the replay reproduce the real Signals rows? (read-only)
        if (o.Has("signal-parity"))
        {
            var fromIst = from + SessionClock.IstOffset;
            var liveSignals = await loader.LoadLiveSignalsAsync(fromIst);
            var liveAudit = await loader.LoadLiveAuditAsync(fromIst);
            var changed = await loader.LoadSettingsChangedSinceAsync(fromIst);
            var parityEvaluator = new EntryEvaluator(live, trading, instruments, SelectorMode.Replay,
                new EntryRules { SkipBreakoutProtection = skipBreakout }, tradingMode);

            // P&L: every replay entry is priced with the grid's exit replay (live stop ladder, real option prices where stored);
            // every real signal with its TradeJournal P&L.
            var parityExit = ExitModel.From(live, trading, exitInputs.AvgDelta, exitInputs.AvgPremium, exitInputs.LotSizes);
            var paritySimulator = new ExecutionSimulator(parityEvaluator, live, parityExit, options, quotes) { StopFillAtClose = o.Get("stop-fill", "") == "close", StopTouchLow = o.Get("stop-touch", "") == "low" };
            var livePnl = await loader.LoadLivePnlBySignalAsync(liveSignals.Select(x => x.Id).ToList());

            // Only decision minutes from --from onward; the earlier bars were loaded to warm the indicators up.
            var parityRows = SignalParityReport.Build(data, parityEvaluator, liveSignals, liveAudit, fromIst, paritySimulator, livePnl);
            var header =
                $"From {fromIst:yyyy-MM-dd} IST; live StrategySettings" +
                (overrides.Count == 0 ? "" : " with overrides " + string.Join(", ", overrides.Select(x => $"{x.Key}{(x.Symbol is null ? "" : "@" + x.Symbol)}={x.Value}"))) +
                $"; trading mode {tradingMode?.ToString() ?? "unknown"}; BreakoutProtection {(skipBreakout ? "forced OFF" : "from StrategySettings")}; " +
                "selector replayed (MarketRegimeGate, Range/Reversal/NoTrade/News routing); 5m confirmation, " +
                "Chop/FastEma/SessionTrend/EarlyTrend/IndexStopFloor/AtrGate replayed; stateful gates are reported, not replayed.";

            if (changed.Count > 0)
            {
                var overridden = overrides.Select(x => (x.Key.ToUpperInvariant(), x.Symbol?.ToUpperInvariant())).ToHashSet();
                header += "\n\n**Settings changed on or after the replay start - the replay uses today's value unless overridden with --set:**\n\n" +
                          "| SettingKey | Symbol | Current value | Active | UpdatedOn | Overridden |\n|---|---|---:|---|---|---|\n" +
                          string.Join("\n", changed.Select(c =>
                              $"| {c.SettingKey} | {c.Symbol ?? "(global)"} | {c.SettingValue} | {c.IsActive} | {c.UpdatedOn:yyyy-MM-dd HH:mm} | " +
                              $"{(overridden.Contains((c.SettingKey.ToUpperInvariant(), c.Symbol?.ToUpperInvariant())) ? "yes" : "NO")} |"));
            }
            SignalParityReport.Write(outDir, parityRows, header);

            // Clean output: only the replay's own trades, simulated like live - one open position across every symbol and
            // both sides (OnePositionGate), same-side cooldown after a stop, live stop ladder, real option prices where stored.
            var cleanRun = paritySimulator.Run(
                SignalParityReport.From(data, fromIst),
                new SimulationOptions(TradeSide.Call, StopModel.Live, PremiumMode.OptionHistoryWhereAvailable, RunCosts(o), BothSides: true,
                    DayRisk: DayRiskLimits.From(trading, tradingMode),
                    StopFillAtClose: o.Get("stop-fill", "") == "close",
                    StopTouchLow: o.Get("stop-touch", "") == "low"));
            SignalParityReport.WriteBacktestTrades(outDir, cleanRun.Trades, liveSignals);
            Console.WriteLine(
                $"Backtest trades: {cleanRun.Trades.Count} ({cleanRun.Trades.Count(t => t.PnlRs > 0)} win / " +
                $"{cleanRun.Trades.Count(t => t.PnlRs <= 0)} loss), gross P&L {cleanRun.Trades.Sum(t => t.PnlRs):+0.00;-0.00} Rs " +
                $"-> {Path.Combine(outDir, "backtest-trades.csv")}");

            Console.WriteLine(SignalParityReport.Summary(parityRows, header));
            Console.WriteLine($"Wrote {Path.Combine(outDir, "signal-parity.csv")} and signal-parity-summary.md ({parityRows.Count} rows).");
            return 0;
        }

        // Tick check (research only, read-only): for live signals the replay also reproduced, did the 1-minute premium path hide a
        // stop that the per-second SignalOptionTicks path shows was touched? Changes no simulated result.
        if (o.Has("tick-check"))
        {
            var fromIst = from + SessionClock.IstOffset;
            List<Trading.Domain.Entities.SignalOptionTick> ticks;
            try
            {
                ticks = await loader.LoadSignalOptionTicksAsync(fromIst);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot read SignalOptionTicks ({ex.GetType().Name}). The table is created by docs/add-signal-option-ticks.sql " +
                                  "(or the AddSignalOptionTicks migration); run it and deploy the Worker first.");
                return 1;
            }
            if (ticks.Count == 0)
            {
                Console.WriteLine($"SignalOptionTicks has no rows from {fromIst:yyyy-MM-dd}. Nothing to check until the Worker has recorded a session.");
                return 0;
            }

            var liveSignals = await loader.LoadLiveSignalsAsync(fromIst);
            var liveAudit = await loader.LoadLiveAuditAsync(fromIst);
            var checkEvaluator = new EntryEvaluator(live, trading, instruments, SelectorMode.Replay,
                new EntryRules { SkipBreakoutProtection = skipBreakout }, tradingMode);
            var checkExit = ExitModel.From(live, trading, exitInputs.AvgDelta, exitInputs.AvgPremium, exitInputs.LotSizes);
            var checkSimulator = new ExecutionSimulator(checkEvaluator, live, checkExit, options, quotes) { StopFillAtClose = o.Get("stop-fill", "") == "close", StopTouchLow = o.Get("stop-touch", "") == "low" };
            var checkLivePnl = await loader.LoadLivePnlBySignalAsync(liveSignals.Select(x => x.Id).ToList());
            var checkRows = SignalParityReport.Build(data, checkEvaluator, liveSignals, liveAudit, fromIst, checkSimulator, checkLivePnl);

            var ticksBySignal = ticks.GroupBy(t => t.SignalId).ToDictionary(g => g.Key, g => (IReadOnlyList<Trading.Domain.Entities.SignalOptionTick>)g.ToList());
            var exits = await loader.LoadSignalExitsAsync(ticksBySignal.Keys.ToList());
            var results = new List<TickCheckRow>();
            foreach (var m in checkRows.Where(r => r.Status == "MATCH" && r.LiveSignalId is { } id && ticksBySignal.ContainsKey(id)))
            {
                var id = m.LiveSignalId!.Value;
                var exit = exits.TryGetValue(id, out var e) ? e : ("", (DateTime?)null);
                results.Add(TickPathCheck.Evaluate(id, m.Symbol, exit.Item1, exit.Item2, m.BacktestExitReason, m.BacktestPnlRs, m.LivePnlRs, ticksBySignal[id]));
            }

            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "tick-check.csv"), TickPathCheck.ToCsv(results));
            File.WriteAllText(Path.Combine(outDir, "tick-check-summary.md"), TickPathCheck.Summary(results));
            Console.WriteLine(TickPathCheck.Summary(results));
            Console.WriteLine($"Wrote {Path.Combine(outDir, "tick-check.csv")} and tick-check-summary.md ({results.Count} rows).");
            return 0;
        }

        // Research only: how exposed are StopNoiseGate blocks to a not-yet-final newest 5m bar (live reads stored rows at the signal second)?
        if (o.Has("bar-lag-check"))
        {
            var fromIst = from + SessionClock.IstOffset;
            var lagLiveSignals = await loader.LoadLiveSignalsAsync(fromIst);
            var lagLiveAudit = await loader.LoadLiveAuditAsync(fromIst);
            var lagEvaluator = new EntryEvaluator(live, trading, instruments, SelectorMode.Replay,
                new EntryRules { SkipBreakoutProtection = skipBreakout }, tradingMode);
            var lagExit = ExitModel.From(live, trading, exitInputs.AvgDelta, exitInputs.AvgPremium, exitInputs.LotSizes);
            var lagSimulator = new ExecutionSimulator(lagEvaluator, live, lagExit, options, quotes) { StopFillAtClose = o.Get("stop-fill", "") == "close", StopTouchLow = o.Get("stop-touch", "") == "low" };
            var lagLivePnl = await loader.LoadLivePnlBySignalAsync(lagLiveSignals.Select(x => x.Id).ToList());
            var lagRows = SignalParityReport.Build(data, lagEvaluator, lagLiveSignals, lagLiveAudit, fromIst, lagSimulator, lagLivePnl);

            var lagResults = new List<BarLagRow>();
            foreach (var r in lagRows.Where(r => r.BacktestBlockedBy == "StopNoiseGate"))
            {
                var sd = data.FirstOrDefault(d => d.Symbol.Equals(r.Symbol, StringComparison.OrdinalIgnoreCase));
                var pt = sd?.Points.FirstOrDefault(x => x.TimeIst == r.MinuteIst);
                if (pt is null) continue;
                var barsN = Math.Max(1, live.GetInt(Trading.Worker.SignalGates.StopNoiseGate.BarsKey, r.Symbol, Trading.Worker.SignalGates.StopNoiseGate.DefaultBars));
                var ratio = (double)live.GetDecimal(Trading.Worker.SignalGates.StopNoiseGate.RatioKey, r.Symbol, Trading.Worker.SignalGates.StopNoiseGate.DefaultRatio);
                var stop = ExitModel.IndexStopPoints(pt.Atr, pt.Atr5m,
                    live.GetBool("EnableAtr5mStop", r.Symbol, false), (double)live.GetDecimal("Atr5mStopMultiplier", r.Symbol, 1.0m),
                    (double)trading.AtrStopLossMultiplier, (double)trading.MinimumIndexStopPoints, (double)trading.MaximumIndexStopPoints);
                lagResults.Add(BarLagCheck.Evaluate(r.Symbol, r.MinuteIst, r.LiveSignalId, r.Status, pt.Bars5m, pt.Closed5m, stop, barsN, ratio));
            }

            Directory.CreateDirectory(outDir);
            File.WriteAllText(Path.Combine(outDir, "bar-lag-check.csv"), BarLagCheck.ToCsv(lagResults));
            File.WriteAllText(Path.Combine(outDir, "bar-lag-check-summary.md"), BarLagCheck.Summary(lagResults));
            Console.WriteLine(BarLagCheck.Summary(lagResults));
            Console.WriteLine($"Wrote {Path.Combine(outDir, "bar-lag-check.csv")} and bar-lag-check-summary.md ({lagResults.Count} rows).");
            return 0;
        }

        var runOptions = new GridRunOptions(
            o.Get("stop", "live").Equals(
                "fixed",
                StringComparison.OrdinalIgnoreCase)
                ? StopModel.Fixed
                : StopModel.Live,

            o.Get("premium", "history").Equals(
                "model",
                StringComparison.OrdinalIgnoreCase)
                ? PremiumMode.ModelOnly
                : PremiumMode.OptionHistoryWhereAvailable,

            o.Has("regime-replay")
                ? SelectorMode.Replay
                : o.Get("selector", "ignore").Equals(
                    "stored",
                    StringComparison.OrdinalIgnoreCase)
                    ? SelectorMode.UseStoredSnapshot
                    : SelectorMode.Ignore,

            RunCosts(o), // costs are always on (Rs20/order + 1pt); pass --cost-per-order 0 --slippage-points 0 for gross

            int.Parse(o.Get("min-trades", "15")));

        var tradeLines = new List<string>
        {
            "Configuration,Symbol,EntryIst,ExitIst,Path,ExitReason,PremiumSource,PnlRs,CostRs,Rsi1m,Rsi5m,Pcr,AtrPct,OpenGapPct,DayChangePct,StretchAtr,Efficiency"
        };

        // NEW: evaluator decision CSV.
        var evaluationLines = new List<string>
        {
            "Configuration,Symbol,TimeIst,Side,Path,SignalProduced,Candidate,BlockedBy,Reason"
        };

        static string Csv(string? value)
        {
            return "\"" + (value ?? "").Replace("\"", "\"\"") + "\"";
        }

        if (o.Has("dump-trades"))
        {
            runOptions = runOptions with
            {
                LogSignals = true,

                OnResult = (cfg, symbol, result) =>
                {
                    // Existing trade export.
                    foreach (var t in result.Trades)
                    {
                        tradeLines.Add(
                            $"{Csv(cfg.Name)},{Csv(symbol)}," +
                            $"{t.EntryUtc + SessionClock.IstOffset:yyyy-MM-dd HH:mm}," +
                            $"{t.ExitUtc + SessionClock.IstOffset:yyyy-MM-dd HH:mm}," +
                            $"{Csv(t.EntryPath)},{Csv(t.ExitReason)},{Csv(t.Source.ToString())}," +
                            $"{t.PnlRs:0.##},{t.CostRs?.ToString("0.##") ?? ""}," +
                            $"{t.Entry?.Rsi1m.ToString("0.##") ?? ""}," +
                            $"{t.Entry?.Rsi5m?.ToString("0.##") ?? ""}," +
                            $"{t.Entry?.Pcr?.ToString("0.###") ?? ""}," +
                            $"{t.Entry?.AtrPct.ToString("0.###") ?? ""}," +
                            $"{t.Entry?.OpenGapPct?.ToString("0.###") ?? ""}," +
                            $"{t.Entry?.DayChangePct.ToString("0.###") ?? ""}," +
                            $"{t.Entry?.StretchAtr.ToString("0.###") ?? ""}," +
                            $"{t.Entry?.Efficiency?.ToString("0.###") ?? ""}");
                    }

                    // NEW: every evaluator decision.
                    foreach (var e in result.Evaluations)
                    {
                        evaluationLines.Add(
                            $"{Csv(cfg.Name)},{Csv(symbol)}," +
                            $"{e.TimeIst:yyyy-MM-dd HH:mm:ss}," +
                            $"{Csv(e.Side)},{Csv(e.Path)}," +
                            $"{e.SignalProduced},{e.Candidate}," +
                            $"{Csv(e.BlockedBy)},{Csv(e.Reason)}");
                    }
                }
            };
        }

        // Existing signal log.
        var signalLines = new List<string>
        {
            "Configuration,Scope,TimeIst,Symbol,Side,Path,Outcome,OpenSymbol,HeldUntilIst,ExitIst,ExitReason,PnlRs,CostRs"
        };

        if (o.Has("signal-log"))
        {
            var previous = runOptions.OnResult;

            runOptions = runOptions with
            {
                LogSignals = true,

                OnResult = (cfg, symbol, result) =>
                {
                    previous?.Invoke(cfg, symbol, result);

                    foreach (var r in result.Log)
                    {
                        signalLines.Add(
                            $"{Csv(cfg.Name)},{Csv(symbol)}," +
                            $"{r.TimeIst:yyyy-MM-dd HH:mm}," +
                            $"{Csv(r.Symbol)},{Csv(r.Side)},{Csv(r.Path)}," +
                            $"{Csv(r.Outcome)},{Csv(r.OpenSymbol)}," +
                            $"{(r.HeldUntilIst.HasValue ? r.HeldUntilIst.Value.ToString("yyyy-MM-dd HH:mm") : "")}," +
                            $"{(r.ExitIst.HasValue ? r.ExitIst.Value.ToString("yyyy-MM-dd HH:mm") : "")}," +
                            $"{Csv(r.ExitReason)},{r.PnlRs?.ToString("0.##") ?? ""}," +
                            $"{r.CostRs?.ToString("0.##") ?? ""}");
                    }
                }
            };
        }

        // Session day types.
        if (o.Has("day-types"))
        {
            var profiles = data
                .SelectMany(d => SessionClassifier.Classify(d.Symbol, d.Bars))
                .ToList();

            var path = Path.Combine(outDir, "day-types.csv");

            using (var w = new StreamWriter(path, false))
            {
                w.WriteLine(
                    "Symbol,Date,Opening,Path,DayType,GapPct,MoveFromOpenPct,RangePct,GapFilled,Open,High,Low,Close,Bars");

                foreach (var pr in profiles.OrderBy(x => x.Date).ThenBy(x => x.Symbol))
                {
                    w.WriteLine(
                        $"{pr.Symbol},{pr.Date:yyyy-MM-dd},{pr.Opening},{pr.Path},{pr.DayType}," +
                        $"{pr.GapPct?.ToString("F3") ?? ""},{pr.MoveFromOpenPct:F3},{pr.RangePct:F3}," +
                        $"{(pr.GapFilled ? 1 : 0)},{pr.Open:F2},{pr.High:F2},{pr.Low:F2},{pr.Close:F2},{pr.Bars}");
                }
            }

            Console.WriteLine(
                $"Wrote {path} ({profiles.Count} sessions)");

            foreach (var g in profiles.GroupBy(x => x.DayType).OrderByDescending(g => g.Count()))
                Console.WriteLine($"  {g.Key,-24} {g.Count(),4} sessions");
        }

        var runner = new GridRunner(
            live,
            trading,
            instruments,
            exitInputs,
            options,
            tradingMode,
            skipBreakout,
            quotes);

        // Existing simulated trades export.
        if (o.Has("sim-trades"))
        {
            var lines = new List<string>
            {
                "Symbol,Side,EntryIst,ExitIst,ExitReason,Source,EntryPremium,PnlRs,CostRs,ReachedT1"
            };

            var sopts = runOptions with
            {
                Costs = runOptions.Costs ?? new CostModel(20, 1),

                OnResult = (cfg, symbol, result) =>
                {
                    if (symbol != "ALL" ||
                        !cfg.Name.EndsWith("LiveForReference"))
                        return;

                    foreach (var t in result.Trades)
                    {
                        lines.Add(
                            $"{t.Symbol},{t.Side}," +
                            $"{t.EntryUtc + SessionClock.IstOffset:yyyy-MM-dd HH:mm}," +
                            $"{t.ExitUtc + SessionClock.IstOffset:yyyy-MM-dd HH:mm}," +
                            $"{t.ExitReason},{t.Source},{t.EntryPremium:0.##}," +
                            $"{t.PnlRs:0.##},{t.CostRs?.ToString("0.##")}," +
                            $"{(t.ReachedT1 ? 1 : 0)}");
                    }
                }
            };

            foreach (var s in sides)
                runner.Run(data, ConfigurationCatalog.BuildBaseRate(s), sopts);

            // Footer: totals over every replayed trade (gross P&L, costs, net) - overall and per symbol.
            var simTrades = lines.Skip(1)
                .Select(l => l.Split(','))
                .Select(f => (Symbol: f[0], Pnl: double.Parse(f[7], CultureInfo.InvariantCulture),
                    Cost: double.TryParse(f[8], NumberStyles.Float, CultureInfo.InvariantCulture, out var c) ? c : 0))
                .ToList();
            string Footer(string label, IReadOnlyList<(string Symbol, double Pnl, double Cost)> t) =>
                $"{label},,,,{t.Count} trades: {t.Count(x => x.Pnl > 0)} win / {t.Count(x => x.Pnl <= 0)} loss," +
                $"gross {t.Sum(x => x.Pnl):+0.##;-0.##;0},net {t.Sum(x => x.Pnl - x.Cost):+0.##;-0.##;0}," +
                $"{t.Sum(x => x.Pnl):+0.##;-0.##;0},{t.Sum(x => x.Cost):0.##},";
            lines.Add("");
            foreach (var g in simTrades.GroupBy(x => x.Symbol).OrderBy(g => g.Key))
                lines.Add(Footer($"TOTAL_{g.Key}", g.ToList()));
            lines.Add(Footer("TOTAL", simTrades));

            var spath = Path.Combine(outDir, "sim-trades.csv");

            File.WriteAllLines(spath, lines);

            Console.WriteLine(
                $"Wrote {spath} ({lines.Count - 1} trades).");

            return 0;
        }

        var rows = new List<GridRow>();

        foreach (var s in sides)
        {
            var configs = SelectConfigurations(o, s, grid);

            Console.WriteLine(
                $"{s}: running {configs.Count} configurations x {data.Count + 1} scopes...");

            rows.AddRange(
                runner.Run(
                    data,
                    configs,
                    runOptions,
                    (i, n) =>
                    {
                        if (i % 25 == 0 || i == n)
                            Console.WriteLine($"  {i}/{n}");
                    }));
        }

        GridWriter.WriteCsv(
            Path.Combine(outDir, "grid.csv"),
            rows);

        var pdfPath = Path.Combine(outDir, "backtest-report.pdf");

        new PdfReportGenerator().Generate(
            Path.Combine(outDir, "grid.csv"),
            pdfPath);

        Console.WriteLine(
            $"PDF generated: {Path.GetFullPath(pdfPath)}");

        if (o.Has("dump-trades"))
        {
            var tradePath = Path.Combine(outDir, "trades.csv");

            File.WriteAllLines(tradePath, tradeLines);

            Console.WriteLine(
                $"Wrote {tradePath} ({tradeLines.Count - 1} trades).");

            // NEW: write every evaluator decision.
            var evaluationPath = Path.Combine(
                outDir,
                "backtest-evaluations.csv");

            File.WriteAllLines(
                evaluationPath,
                evaluationLines);

            Console.WriteLine(
                $"Wrote {evaluationPath} ({evaluationLines.Count - 1} evaluations).");
        }

        if (o.Has("signal-log"))
        {
            var signalPath = Path.Combine(outDir, "signals.csv");

            File.WriteAllLines(signalPath, signalLines);

            Console.WriteLine(
                $"Wrote {signalPath} ({signalLines.Count - 1} signals).");
        }

        var assumptions =
            $"Stop model {runOptions.StopModel}; premium {runOptions.PremiumMode} " +
            "(real option prices exist from 2026-10-07 only; earlier trades use the real entry quote stored in Signals / IndicatorSnapshots " +
            "x index move where one exists, else index move x average delta); " +
            $"selector {runOptions.SelectorMode}; costs " +
            $"{(runOptions.Costs is null ? "NOT modelled (NetProfitAfterCosts = NULL)" : "modelled")}; " +
            "settings = current StrategySettings applied to every day; " +
            "bar-close granularity (stops are checked at 1m closes); " +
            "not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).";

        GridWriter.WriteRanking(
            Path.Combine(outDir, "ranking.md"),
            rows,
            runOptions.MinTradesPerPeriod,
            assumptions);

        Console.WriteLine(
            $"Wrote {Path.Combine(outDir, "grid.csv")} and ranking.md ({rows.Count} rows).");

        return 0;
    }

    /// <summary>
    /// Live settings whose effect the replay cannot reproduce (or reproduces although production does not), so a run that
    /// claims "live rules" is not silently different from production.
    /// </summary>
    private static IEnumerable<string> ParityWarnings(BacktestSettings live, bool selectorReplayed)
    {
        foreach (var symbol in ExitModel.SymbolNames)
        {
            foreach (var (key, why) in new[]
            {
                ("EnableMarketDirectionGate", "MarketDirectionGate is live but not replayed (reads the *_FUT_5m series)"),
                ("EnablePutS1Gate", "PutS1Gate is live but not replayed (needs the entry leg's option delta)"),
                ("EnableStaleEntryGate", "StaleEntryGate is live but not replayed (needs feed lag; ideal data never trips it)"),
            })
            {
                if (live.GetBool(key, symbol, false))
                    yield return $"{symbol}: {key}=1 - {why}.";
            }

            foreach (var (key, gate) in new[]
            {
                ("EnableNoTradeWindowGate", nameof(Trading.Worker.SignalGates.NoTradeWindowGate)),
                ("EnableChopGate", nameof(Trading.Worker.SignalGates.ChopGate)),
                ("EnableFastEmaGate", nameof(Trading.Worker.SignalGates.FastEmaGate)),
                ("EnableVwapSideGate", nameof(Trading.Worker.SignalGates.VwapSideGate)),
                ("EnableSessionTrendGate", nameof(Trading.Worker.SignalGates.SessionTrendGate)),
                ("EnableHandoffCooldownGate", nameof(Trading.Worker.SignalGates.HandoffCooldownGate)),
                ("EnableEarlyTrendGate", nameof(Trading.Worker.SignalGates.EarlyTrendGate)),
            })
            {
                if (live.GetBool(key, symbol, false) && !Trading.Backtest.Entry.LiveGates.Names.Contains(gate))
                    yield return $"{symbol}: {key}=1 but {gate} is NOT in SignalWorker.PostGates - live ignores it, so the replay skips it too.";
            }

            if (live.IsSettingActive("MinDirectionalConfidence", symbol))
                yield return $"{symbol}: MinDirectionalConfidence is active - the confidence floor is not replayed.";

            if (live.GetBool("EnableCrossIndexAgreementGate", symbol, false))
                yield return $"{symbol}: CrossIndexAgreementGate is replayed on the simulated trades (backtest-trades and grid) from the other active indices' closed 1m bars, " +
                             "not on the per-minute signal-parity rows; the active set is today's TradingInstruments.";

            if (live.GetBool("EnableConsecutiveLossGate", symbol, true))
                yield return $"{symbol}: ConsecutiveLossGate is on - replayed on the simulated trades (backtest-trades and grid), " +
                             "not on the per-minute signal-parity rows.";
        }

        if (!selectorReplayed)
            yield return "selector not replayed: MarketRegimeGate and the Range/Reversal/NoTrade/News routing (BlockRangeMarketState) are skipped. Pass --regime-replay for production behaviour.";
    }

    /// <summary>"KEY=VALUE[@SYMBOL],..." -> (key, symbol or null for the global row, value).</summary>
    private static List<(string Key, string? Symbol, decimal Value)> ParseOverrides(string csv)
    {
        var result = new List<(string, string?, decimal)>();
        foreach (var item in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var eq = item.IndexOf('=');
            if (eq <= 0)
                throw new ArgumentException($"--set item '{item}' is not KEY=VALUE[@SYMBOL]");
            var key = item[..eq].Trim();
            var rest = item[(eq + 1)..];
            var at = rest.IndexOf('@');
            var symbol = at >= 0 ? rest[(at + 1)..].Trim().ToUpperInvariant() : null;
            var value = decimal.Parse(at >= 0 ? rest[..at] : rest, System.Globalization.CultureInfo.InvariantCulture);
            result.Add((key, string.IsNullOrEmpty(symbol) ? null : symbol, value));
        }
        return result;
    }

    private static decimal[] Decimals(string csv) =>
        csv.Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries)
            .Select(v => decimal.Parse(
                v,
                System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();

    private static IReadOnlyList<BacktestConfiguration> SelectConfigurations(
        Args o,
        TradeSide s,
        GridOptions grid) =>
        o.Has("early-trend-gapdays") ? ConfigurationCatalog.BuildEarlyTrendGapDays(grid)
        : o.Has("oi-gate") ? ConfigurationCatalog.BuildOiGateSweep(s)
        : o.Has("stop-floor") ? ConfigurationCatalog.BuildStopFloorSweep(s)
        : o.Has("t2-partial") ? ConfigurationCatalog.BuildT2PartialComparison(s)
        : o.Has("exit-geometry") ? ConfigurationCatalog.BuildExitGeometrySweep(s)
        : o.Has("proposed-candidates") ? ConfigurationCatalog.BuildProposedCandidates(s)
        : o.Has("session-trend") ? ConfigurationCatalog.BuildSessionTrendSweep(s)
        : o.Has("breakout-level") ? ConfigurationCatalog.BuildBreakoutLevel(s)
        : o.Has("early-cross") ? ConfigurationCatalog.BuildEarlyCross()
        : o.Has("selector-states") ? ConfigurationCatalog.BuildSelectorStateSweep(s)
        : o.Has("chain-loo") ? ConfigurationCatalog.BuildChainLeaveOneOut(s)
        : o.Has("live-interchange") ? ConfigurationCatalog.BuildLiveInterchange(s)
        : o.Has("rated") ? ConfigurationCatalog.BuildRatedDirectionSweep(s)
        : o.Has("thinning") ? ConfigurationCatalog.BuildThinningControl(s)
        : o.Has("gapdown-put") ? ConfigurationCatalog.BuildGapDownPutSweep(s)
        : o.Has("opening-band") ? ConfigurationCatalog.BuildOpeningBandSweep(s)
        : o.Has("base-rate") ? ConfigurationCatalog.BuildBaseRate(s)
        : o.Has("efficiency-sweep") ? ConfigurationCatalog.BuildEfficiencySweep(s, grid)
        : o.Has("stretch-by-opening") ? ConfigurationCatalog.BuildStretchByOpening(s, grid)
        : o.Has("shared-slot") ? ConfigurationCatalog.BuildSharedSlot()
        : o.Has("slot-sweep") ? ConfigurationCatalog.BuildSlotSweep(s)
        : o.Has("stretch-sweep") ? ConfigurationCatalog.BuildStretchSweep(s, grid)
        : o.Has("gapup-morning-exception") ? ConfigurationCatalog.BuildGapUpMorningException(grid)
        : o.Has("early-trend-exception") ? ConfigurationCatalog.BuildEarlyTrendException(grid)
        : o.Has("gapup-call-gate") ? ConfigurationCatalog.BuildGapUpCallGate()
        : o.Has("gapup-momentum") ? ConfigurationCatalog.BuildGapUpMomentum()
        : o.Has("fast-ema") ? ConfigurationCatalog.BuildFastEma(s, grid)
        : o.Has("opening-types") ? ConfigurationCatalog.BuildOpeningTypes(s, grid)
        : o.Has("put-early-trend") ? ConfigurationCatalog.BuildPutEarlyTrend()
        : o.Has("trend-strategies") ? ConfigurationCatalog.BuildTrendStrategies()
        : o.Has("two-by-two") ? ConfigurationCatalog.BuildBankNiftyTwoByTwo()
        : o.Has("gapup-proposal") ? ConfigurationCatalog.BuildGapUpProposal(s)
        : o.Has("relaxations") ? ConfigurationCatalog.BuildRelaxations(s, grid)
        : ConfigurationCatalog.Build(s, grid, !o.Has("no-grid"));

    private static string? ResolveConnection(Args o)
    {
        if (o.Has("connection"))
            return o.Get("connection", "");

        var env = Environment.GetEnvironmentVariable(
            "TRADING_BACKTEST_CONNECTION");

        if (!string.IsNullOrWhiteSpace(env))
            return env;

        var worker = FindWorkerDirectory();

        if (worker is null)
            return null;

        var cfg = new ConfigurationBuilder()
            .AddJsonFile(
                Path.Combine(worker, "appsettings.json"),
                optional: true)
            .AddJsonFile(
                Path.Combine(worker, "appsettings.Production.json"),
                optional: true)
            .Build();

        return cfg.GetConnectionString("DefaultConnection");
    }

    private static TradingSettings LoadTradingSettings()
    {
        var worker = FindWorkerDirectory();

        if (worker is null)
            return new TradingSettings();

        var cfg = new ConfigurationBuilder()
            .AddJsonFile(
                Path.Combine(worker, "appsettings.json"),
                optional: true)
            .Build();

        return cfg.GetSection("Trading").Get<TradingSettings>()
               ?? new TradingSettings();
    }

    private static string? FindWorkerDirectory()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
             dir is not null;
             dir = dir.Parent)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src",
                "Trading.Worker");

            if (Directory.Exists(candidate))
                return candidate;
        }

        return null;
    }
}

internal sealed class Args
{
    private readonly Dictionary<string, string> _values =
        new(StringComparer.OrdinalIgnoreCase);

    public static Args Parse(string[] args)
    {
        var a = new Args();

        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--"))
                continue;

            var key = args[i][2..];

            a._values[key] =
                i + 1 < args.Length && !args[i + 1].StartsWith("--")
                    ? args[++i]
                    : "true";
        }

        return a;
    }

    public bool Has(string key) =>
        _values.ContainsKey(key);

    public string Get(string key, string fallback) =>
        _values.TryGetValue(key, out var v)
            ? v
            : fallback;
}
