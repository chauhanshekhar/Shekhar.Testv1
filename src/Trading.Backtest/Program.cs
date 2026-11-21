
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Net.Sockets;
using Trading.Backtest.Configurations;
using Trading.Backtest.Data;
using Trading.Backtest.Discovery;
using Trading.Backtest.Entry;
using Trading.Backtest.Indicators;
using Trading.Backtest.Reporting;
using Trading.Backtest.Simulation;
using Trading.Infrastructure;

namespace Trading.Backtest;

internal static class Program
{
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
                var bars = await loader.LoadBarsAsync($"{symbol}_FUT_1m", from);

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
        var instruments = await loader.LoadInstrumentsAsync();
        var exitInputs = await loader.LoadExitInputsAsync(instruments);
        var trading = LoadTradingSettings();

        Console.WriteLine(
            "Loading candles, snapshots and option history (read-only)...");

        var snapshots = await loader.LoadSnapshotsAsync(from);
        var optionRows = await loader.LoadOptionHistoryAsync();

        var options = optionRows.Count > 0
            ? new OptionHistoryIndex(optionRows)
            : null;

        var data = new List<SymbolData>();
        var bars5BySymbol = new Dictionary<string, Bar[]>();

        foreach (var symbol in ExitModel.SymbolNames)
        {
            var b1 = await loader.LoadBarsAsync($"{symbol}_FUT_1m", from);
            var b5 = await loader.LoadBarsAsync($"{symbol}_FUT_5m", from);
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

            if (o.Has("regime-replay"))
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

            o.Has("cost-per-order")
                ? new CostModel(
                    double.Parse(o.Get("cost-per-order", "0")),
                    double.Parse(o.Get("slippage-points", "0")))
                : null,

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
            options);

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
            "(real option prices exist from 2026-10-07 only, earlier trades use index move x average delta); " +
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
