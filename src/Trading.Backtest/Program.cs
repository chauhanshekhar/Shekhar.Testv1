using Microsoft.Extensions.Configuration;
using Trading.Backtest.Configurations;
using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Indicators;
using Trading.Backtest.Reporting;
using Trading.Backtest.Simulation;
using Trading.Infrastructure;

namespace Trading.Backtest;

/// <summary>
/// Read-only historical replay of the CALL / PUT entry rules. It never writes to the database, never changes StrategySettings,
/// and never starts a worker. Usage (repo root):
///   dotnet run --project src/Trading.Backtest -- --side BOTH --out docs/research/yyyy-mm-dd-backtest-grid
/// Options: --from yyyy-MM-dd (UTC, default 2026-07-10)  --side CALL|PUT|BOTH  --stop live|fixed  --premium model|history
///          --selector ignore|stored  --cost-per-order Rs  --slippage-points P  --no-grid  --max-grid N  --min-trades N
///          --connection "..." (else TRADING_BACKTEST_CONNECTION, else the Worker appsettings files)  --list-configs  --early-cross (CALL entry within N min of the 1m EMA turn)  --selector-states (which MarketState blocks, and allow Directional in Range/Reversal)  --chain-loo (CALL/PUT AND-chain with one condition removed at a time)  --live-interchange (every on/off combination of the live entry conditions)  --rated (no-RSI CALL vs PUT score strategy, MinScore x MinMargin grid)  --regime-replay (replay MarketRegimeGate + selector)  --relaxations (the gap-up experiments)  --gapup-proposal (G1/G2/G3 from the pasted proposal)  --two-by-two (BANKNIFTY CALL gate x RSI ceiling)  --trend-strategies (S1 momentum, S2 gap-hold)  --dump-trades (write trades.csv)  --gap-neutral-rsi5 (experiment: remove overnight gaps from the 5m RSI)  --call-rsi-ceilings 65,70,80 (also --call-rsi-floors, --put-rsi-floors, --put-rsi-ceilings)  --exit-geometry (stop x target ATR width grid on the live entries)  --proposed-candidates (PUT: two snapshot-derived candidates, replace / and-chain arms)  --opening-types (six groups: GapUp/GapDown/Normal x CALL/PUT, each swept on its own)  --stretch-sweep (late-entry cap: ATRs beyond EMA20 at entry)  --slot-sweep (the one-position rule: concurrency, per-symbol slots, handoff cooldown)  --day-types (write day-types.csv: opening x continuation/reversal/range per session)  --efficiency-sweep (stretch cap only on round-tripping sessions) --efficiency 0.25,0.35  --stretch-max 0.6,0.7,0.8 (also --stretch-min)  --parity [--parity-from yyyy-MM-dd] (compare rebuilt indicators with stored snapshots, then stop)
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var o = Args.Parse(args);
        var side = o.Get("side", "BOTH").ToUpperInvariant();
        var sides = side switch { "CALL" => [TradeSide.Call], "PUT" => [TradeSide.Put], _ => new[] { TradeSide.Call, TradeSide.Put } };
        var grid = new GridOptions
        {
            MaxGridConfigurations = int.Parse(o.Get("max-grid", "600")),
            CallRsiFloors = o.Has("call-rsi-floors") ? Decimals(o.Get("call-rsi-floors", "")) : new GridOptions().CallRsiFloors,
            CallRsiCeilings = o.Has("call-rsi-ceilings") ? Decimals(o.Get("call-rsi-ceilings", "")) : new GridOptions().CallRsiCeilings,
            PutRsiFloors = o.Has("put-rsi-floors") ? Decimals(o.Get("put-rsi-floors", "")) : new GridOptions().PutRsiFloors,
            PutRsiCeilings = o.Has("put-rsi-ceilings") ? Decimals(o.Get("put-rsi-ceilings", "")) : new GridOptions().PutRsiCeilings,
            EfficiencyThresholds = o.Has("efficiency") ? Decimals(o.Get("efficiency", "")) : new GridOptions().EfficiencyThresholds,
            StretchMaxAtr = o.Has("stretch-max") ? Decimals(o.Get("stretch-max", "")) : new GridOptions().StretchMaxAtr,
            StretchMinAtr = o.Has("stretch-min") ? Decimals(o.Get("stretch-min", "")) : new GridOptions().StretchMinAtr,
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
            Console.Error.WriteLine("No connection string: pass --connection, set TRADING_BACKTEST_CONNECTION, or provide the Worker appsettings.");
            return 2;
        }

        var from = DateTime.Parse(o.Get("from", "2026-07-10"), null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
        var outDir = o.Get("out", Path.Combine("docs", "research", $"{DateTime.Now:yyyy-MM-dd}-backtest-grid"));
        Directory.CreateDirectory(outDir);

        var loader = new HistoricalDataLoader(connection);
        var live = await loader.LoadSettingsAsync();
        var instruments = await loader.LoadInstrumentsAsync();
        var exitInputs = await loader.LoadExitInputsAsync(instruments);
        var trading = LoadTradingSettings();

        Console.WriteLine("Loading candles, snapshots and option history (read-only)...");
        var snapshots = await loader.LoadSnapshotsAsync(from);
        var optionRows = await loader.LoadOptionHistoryAsync();
        var options = optionRows.Count > 0 ? new OptionHistoryIndex(optionRows) : null;

        var data = new List<SymbolData>();
        foreach (var symbol in ExitModel.SymbolNames)
        {
            var b1 = await loader.LoadBarsAsync($"{symbol}_1m", from);
            var b5 = await loader.LoadBarsAsync($"{symbol}_5m", from);
            if (b1.Count == 0) { Console.WriteLine($"  {symbol}: no 1m bars, skipped"); continue; }
            var points = MarketSeriesBuilder.Build(symbol, b1, b5, o.Has("gap-neutral-rsi5"));
            points = MarketSeriesBuilder.AttachSnapshots(points, snapshots.Where(s => s.Symbol == symbol).ToList(), TimeSpan.FromMinutes(3));
            if (o.Has("regime-replay"))
                points = RegimeEnricher.Attach(symbol, points, b1.OrderBy(b => b.OpenUtc).ToArray(), trading);
            var interval = instruments.FirstOrDefault(i => string.Equals(i.Symbol, symbol, StringComparison.OrdinalIgnoreCase))?.StrikeInterval ?? 50;
            data.Add(new SymbolData { Symbol = symbol, Points = points.ToArray(), Bars = b1.OrderBy(b => b.OpenUtc).ToArray(), StrikeInterval = interval });
            Console.WriteLine($"  {symbol}: {b1.Count} 1m bars, {b5.Count} 5m bars, {points.Count} decision minutes, " +
                              $"{points.Select(p => p.TimeIst.Date).Distinct().Count()} sessions, snapshot join {points.Count(p => p.Pcr is not null)} rows");
        }
        if (data.Count == 0) { Console.Error.WriteLine("No candle data."); return 3; }

        if (o.Has("parity"))
        {
            var parityFrom = DateTime.Parse(o.Get("parity-from", "2026-10-02"));
            var snaps = await ParityCheck.LoadAsync(loader.Options, parityFrom);
            Console.WriteLine($"Parity: rebuilt indicators vs stored snapshots (last second of each minute, sessions from {parityFrom:yyyy-MM-dd}, {snaps.Count} snapshots)");
            foreach (var d in data)
                foreach (var line in ParityCheck.Compare(d.Symbol, d.Points, snaps))
                    Console.WriteLine(line);
            foreach (var d in data)
            {
                var withBoth = d.Points.Where(p => p.ReplayedMarketState is not null && !string.IsNullOrEmpty(p.MarketState) && p.TimeIst >= parityFrom).ToList();
                if (withBoth.Count == 0) continue;
                var stateMatch = withBoth.Count(p => string.Equals(p.ReplayedMarketState, p.MarketState, StringComparison.OrdinalIgnoreCase));
                var regimeMatch = withBoth.Count(p => p.StoredMarketRegime is not null && p.StoredMarketRegime.StartsWith(p.RegimeTrend + "/" + p.RegimeVolatility, StringComparison.OrdinalIgnoreCase));
                foreach (var g in withBoth.Where(p => !string.Equals(p.ReplayedMarketState, p.MarketState, StringComparison.OrdinalIgnoreCase))
                             .GroupBy(p => $"stored '{(string.IsNullOrEmpty(p.MarketState) ? "(empty)" : p.MarketState)}' -> rebuilt '{p.ReplayedMarketState}'")
                             .OrderByDescending(g => g.Count()).Take(4))
                    Console.WriteLine($"      {d.Symbol,-10} differs: {g.Key} x{g.Count()}");
                Console.WriteLine($"  {d.Symbol,-10} MarketState rebuilt = stored on {100.0 * stateMatch / withBoth.Count:F1}%   MarketRegime rebuilt = stored on {100.0 * regimeMatch / withBoth.Count:F1}%   (n={withBoth.Count})");
            }
            return 0;
        }

        var runOptions = new GridRunOptions(
            o.Get("stop", "live").Equals("fixed", StringComparison.OrdinalIgnoreCase) ? StopModel.Fixed : StopModel.Live,
            o.Get("premium", "history").Equals("model", StringComparison.OrdinalIgnoreCase) ? PremiumMode.ModelOnly : PremiumMode.OptionHistoryWhereAvailable,
            o.Has("regime-replay") ? SelectorMode.Replay : o.Get("selector", "ignore").Equals("stored", StringComparison.OrdinalIgnoreCase) ? SelectorMode.UseStoredSnapshot : SelectorMode.Ignore,
            o.Has("cost-per-order") ? new CostModel(double.Parse(o.Get("cost-per-order", "0")), double.Parse(o.Get("slippage-points", "0"))) : null,
            int.Parse(o.Get("min-trades", "15")));

        var tradeLines = new List<string> { "Configuration,Symbol,EntryIst,ExitIst,Path,ExitReason,PremiumSource,PnlRs,CostRs,Rsi1m,Rsi5m,Pcr,AtrPct,OpenGapPct,DayChangePct,StretchAtr,Efficiency" };
        if (o.Has("dump-trades"))
            runOptions = runOptions with
            {
                OnResult = (cfg, symbol, result) =>
                {
                    foreach (var t in result.Trades)
                        tradeLines.Add($"{cfg.Name},{symbol},{t.EntryUtc + SessionClock.IstOffset:yyyy-MM-dd HH:mm},{t.ExitUtc + SessionClock.IstOffset:yyyy-MM-dd HH:mm},{t.EntryPath},{t.ExitReason},{t.Source},{t.PnlRs:0.##},{t.CostRs?.ToString("0.##") ?? ""}," +
                            $"{t.Entry?.Rsi1m.ToString("0.##") ?? ""},{t.Entry?.Rsi5m?.ToString("0.##") ?? ""}," +
                            $"{t.Entry?.Pcr?.ToString("0.###") ?? ""},{t.Entry?.AtrPct.ToString("0.###") ?? ""}," +
                            $"{t.Entry?.OpenGapPct?.ToString("0.###") ?? ""},{t.Entry?.DayChangePct.ToString("0.###") ?? ""}," +
                            $"{t.Entry?.StretchAtr.ToString("0.###") ?? ""},{t.Entry?.Efficiency?.ToString("0.###") ?? ""}");
                },
            };

        // --signal-log: one row per signal minute (ENTERED or the gate that rejected it, what held it and until when).
        var signalLines = new List<string> { "Configuration,Scope,TimeIst,Symbol,Side,Path,Outcome,OpenSymbol,HeldUntilIst,ExitIst,ExitReason,PnlRs,CostRs" };
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
                        signalLines.Add($"{cfg.Name},{symbol},{r.TimeIst:yyyy-MM-dd HH:mm},{r.Symbol},{r.Side},{r.Path},{r.Outcome},{r.OpenSymbol},{r.HeldUntilIst:yyyy-MM-dd HH:mm},{r.ExitIst:yyyy-MM-dd HH:mm},{r.ExitReason},{r.PnlRs:0.##},{r.CostRs:0.##}");
                },
            };
        }

        // Session day types (REPORTING ONLY - computed from completed sessions, see SessionClassifier).
        if (o.Has("day-types"))
        {
            var profiles = data.SelectMany(d => SessionClassifier.Classify(d.Symbol, d.Bars)).ToList();
            var path = Path.Combine(outDir, "day-types.csv");
            Directory.CreateDirectory(outDir);
            using (var w = new StreamWriter(path, false))
            {
                w.WriteLine("Symbol,Date,Opening,Path,DayType,GapPct,MoveFromOpenPct,RangePct,GapFilled,Open,High,Low,Close,Bars");
                foreach (var pr in profiles.OrderBy(x => x.Date).ThenBy(x => x.Symbol))
                    w.WriteLine($"{pr.Symbol},{pr.Date:yyyy-MM-dd},{pr.Opening},{pr.Path},{pr.DayType}," +
                                $"{pr.GapPct?.ToString("F3") ?? ""},{pr.MoveFromOpenPct:F3},{pr.RangePct:F3}," +
                                $"{(pr.GapFilled ? 1 : 0)},{pr.Open:F2},{pr.High:F2},{pr.Low:F2},{pr.Close:F2},{pr.Bars}");
            }
            Console.WriteLine($"Wrote {path} ({profiles.Count} sessions)");
            foreach (var g in profiles.GroupBy(x => x.DayType).OrderByDescending(g => g.Count()))
                Console.WriteLine($"  {g.Key,-24} {g.Count(),4} sessions");
        }

        var runner = new GridRunner(live, trading, instruments, exitInputs, options);
        var rows = new List<GridRow>();
        foreach (var s in sides)
        {
            var configs = SelectConfigurations(o, s, grid);
            Console.WriteLine($"{s}: running {configs.Count} configurations x {data.Count + 1} scopes...");
            rows.AddRange(runner.Run(data, configs, runOptions, (i, n) => { if (i % 25 == 0 || i == n) Console.WriteLine($"  {i}/{n}"); }));
        }

        GridWriter.WriteCsv(Path.Combine(outDir, "grid.csv"), rows);
        if (o.Has("dump-trades"))
            File.WriteAllLines(Path.Combine(outDir, "trades.csv"), tradeLines);
        if (o.Has("signal-log"))
            File.WriteAllLines(Path.Combine(outDir, "signals.csv"), signalLines);
        var assumptions = $"Stop model {runOptions.StopModel}; premium {runOptions.PremiumMode} (real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); " +
                          $"selector {runOptions.SelectorMode}; costs {(runOptions.Costs is null ? "NOT modelled (NetProfitAfterCosts = NULL)" : "modelled")}; settings = current StrategySettings applied to every day; " +
                          "bar-close granularity (stops are checked at 1m closes); not replayed: KillSwitch, EventBlackout, StaleData, MarketRegime, Risk, RiskReward, Spread, StaleEntry (ideal data).";
        GridWriter.WriteRanking(Path.Combine(outDir, "ranking.md"), rows, runOptions.MinTradesPerPeriod, assumptions);
        Console.WriteLine($"Wrote {Path.Combine(outDir, "grid.csv")} and ranking.md ({rows.Count} rows).");
        return 0;
    }

    private static decimal[] Decimals(string csv) =>
        csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .Select(v => decimal.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).ToArray();

    /// <summary>The configuration set the flags ask for. Shared by --list-configs and the run so the two can never disagree.</summary>
    private static IReadOnlyList<BacktestConfiguration> SelectConfigurations(Args o, TradeSide s, GridOptions grid) =>
        o.Has("early-trend-gapdays") ? ConfigurationCatalog.BuildEarlyTrendGapDays(grid)
        : o.Has("oi-gate") ? ConfigurationCatalog.BuildOiGateSweep(s)
        : o.Has("stop-floor") ? ConfigurationCatalog.BuildStopFloorSweep(s)
        : o.Has("t2-partial") ? ConfigurationCatalog.BuildT2PartialComparison(s)
        : o.Has("exit-geometry") ? ConfigurationCatalog.BuildExitGeometrySweep(s)
        : o.Has("proposed-candidates") ? ConfigurationCatalog.BuildProposedCandidates(s)
        : o.Has("opening-rsi") ? ConfigurationCatalog.BuildOpeningRsiSweep(s)
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
        : o.Has("gapup-put-band") ? ConfigurationCatalog.BuildGapUpPutBand()
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
        if (o.Has("connection")) return o.Get("connection", "");
        var env = Environment.GetEnvironmentVariable("TRADING_BACKTEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(env)) return env;

        var worker = FindWorkerDirectory();
        if (worker is null) return null;
        var cfg = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(worker, "appsettings.json"), optional: true)
            .AddJsonFile(Path.Combine(worker, "appsettings.Production.json"), optional: true)
            .Build();
        return cfg.GetConnectionString("DefaultConnection");
    }

    private static TradingSettings LoadTradingSettings()
    {
        var worker = FindWorkerDirectory();
        if (worker is null) return new TradingSettings();
        var cfg = new ConfigurationBuilder().AddJsonFile(Path.Combine(worker, "appsettings.json"), optional: true).Build();
        return cfg.GetSection("Trading").Get<TradingSettings>() ?? new TradingSettings();
    }

    private static string? FindWorkerDirectory()
    {
        for (var dir = new DirectoryInfo(Directory.GetCurrentDirectory()); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "Trading.Worker");
            if (Directory.Exists(candidate)) return candidate;
        }
        return null;
    }
}

internal sealed class Args
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public static Args Parse(string[] args)
    {
        var a = new Args();
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) continue;
            var key = args[i][2..];
            a._values[key] = i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[++i] : "true";
        }
        return a;
    }

    public bool Has(string key) => _values.ContainsKey(key);
    public string Get(string key, string fallback) => _values.TryGetValue(key, out var v) ? v : fallback;
}
