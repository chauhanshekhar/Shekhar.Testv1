using Trading.Backtest.Configurations;
using Trading.Backtest.Data;
using Trading.Backtest.Entry;
using Trading.Backtest.Settings;
using Trading.Backtest.Simulation;
using Trading.Domain.Entities;
using Trading.Infrastructure;

namespace Trading.Backtest.Reporting;

/// <summary>One row of the comparison grid: a configuration on one symbol (or ALL symbols together under one position).</summary>
public sealed record GridRow(
    string Configuration, string Symbol, string TradeType,
    int TotalSignals, int BlockedCount, PerformanceMetrics All,
    PerformanceMetrics Train, PerformanceMetrics Validation, PerformanceMetrics Test,
    string BlockedDetail, int RealPremiumTrades, int DataUnavailable, int OiUnverifiable, string Coverage);

public sealed record GridRunOptions(
    StopModel StopModel = StopModel.Live,
    PremiumMode PremiumMode = PremiumMode.OptionHistoryWhereAvailable,
    SelectorMode SelectorMode = SelectorMode.Ignore,
    CostModel? Costs = null,
    int MinTradesPerPeriod = 15,
    Action<BacktestConfiguration, string, SimulationResult>? OnResult = null,
    bool LogSignals = false);

/// <summary>Runs every configuration on every symbol alone and on all symbols together, then splits each result by period.</summary>
public sealed class GridRunner
{
    private readonly BacktestSettings _live;
    private readonly TradingSettings _trading;
    private readonly IReadOnlyList<TradingInstrument> _instruments;
    private readonly ExitModelInputs _exitInputs;
    private readonly OptionHistoryIndex? _options;

    public GridRunner(BacktestSettings live, TradingSettings trading, IReadOnlyList<TradingInstrument> instruments,
        ExitModelInputs exitInputs, OptionHistoryIndex? options)
    {
        _live = live; _trading = trading; _instruments = instruments; _exitInputs = exitInputs; _options = options;
    }

    public List<GridRow> Run(IReadOnlyList<SymbolData> data, IEnumerable<BacktestConfiguration> configs, GridRunOptions opt,
        Action<int, int>? progress = null)
    {
        var sessions = data.SelectMany(d => d.Points.Select(p => DateOnly.FromDateTime(p.TimeIst))).Distinct().ToList();
        var split = PeriodSplit.Create(sessions);
        var rows = new List<GridRow>();
        var configList = configs.ToList();
        var done = 0;

        foreach (var config in configList)
        {
            var settings = config.Apply(_live);
            var evaluator = new EntryEvaluator(settings, _trading, _instruments, opt.SelectorMode, config.Rules);
            var exit = ExitModel.From(settings, _trading, _exitInputs.AvgDelta, _exitInputs.AvgPremium, _exitInputs.LotSizes);
            if (config.Exit is { } geometry)
                exit = geometry.ApplyTo(exit);
            var sim = new ExecutionSimulator(evaluator, settings, exit, _options);
            var simOptions = new SimulationOptions(config.Side, opt.StopModel, opt.PremiumMode, opt.Costs,
                MaxTradesPerSymbolDay: config.Sampling?.MaxTradesPerSymbolDay ?? config.Rules?.Trend?.MaxTradesPerSymbolDay,
                MaxConcurrentPositions: config.Slots?.MaxConcurrent ?? 1,
                PerSymbolSlots: config.Slots?.PerSymbol ?? false,
                DisableHandoffCooldown: config.Slots?.NoHandoffCooldown ?? false,
                KeepEveryNth: config.Sampling?.KeepEveryNth,
                BothSides: config.Slots?.SharedBothSides ?? false,
                LogSignals: opt.LogSignals);

            foreach (var symbol in data.Select(d => d.Symbol).Append("ALL"))
            {
                var subset = symbol == "ALL" ? data : data.Where(d => d.Symbol == symbol).ToList();
                Console.WriteLine(
    $"SIM INPUT {symbol}: " +
    $"Symbols={subset.Count}, " +
    $"Points={subset.Sum(d => d.Points.Length)}, " +
    $"First={subset.SelectMany(d => d.Points).Min(p => p.TimeIst):yyyy-MM-dd HH:mm:ss}, " +
    $"Last={subset.SelectMany(d => d.Points).Max(p => p.TimeIst):yyyy-MM-dd HH:mm:ss}");
                var result = sim.Run(subset, simOptions);
                var nov19 = result.Evaluations
    .Where(e => e.TimeIst.Date == new DateTime(2026, 11, 19))
    .OrderBy(e => e.TimeIst)
    .ToList();

                Console.WriteLine(
                    $"NOV19 {symbol}: Count={nov19.Count}, " +
                    $"First={nov19.FirstOrDefault()?.TimeIst:yyyy-MM-dd HH:mm:ss}, " +
                    $"Last={nov19.LastOrDefault()?.TimeIst:yyyy-MM-dd HH:mm:ss}");
                opt.OnResult?.Invoke(config, symbol, result);
                rows.Add(ToRow(config, symbol, result, split, subset));
            }
            progress?.Invoke(++done, configList.Count);
        }
        return rows;
    }

    private static GridRow ToRow(BacktestConfiguration c, string symbol, SimulationResult r, PeriodSplit split, IReadOnlyList<SymbolData> subset)
    {
        var points = subset.Sum(d => d.Points.Length);
        var withPcr = subset.Sum(d => d.Points.Count(p => p.Pcr is not null));
        var withState = subset.Sum(d => d.Points.Count(p => p.MarketState is not null));
        var coverage = $"points={points}; pcr={(points == 0 ? 0 : 100.0 * withPcr / points):F0}%; marketState={(points == 0 ? 0 : 100.0 * withState / points):F0}%";

        return new GridRow(
            c.Name, symbol, c.TradeType, r.TotalSignals, r.BlockedCount,
            PerformanceMetrics.Compute(r.Trades),
            PerformanceMetrics.Compute(split.InTrain(r.Trades)),
            PerformanceMetrics.Compute(split.InValidation(r.Trades)),
            PerformanceMetrics.Compute(split.InTest(r.Trades)),
            string.Join("; ", r.BlockedBy.OrderByDescending(x => x.Value).Select(x => $"{x.Key}={x.Value}")),
            r.RealPremiumTrades, r.DataUnavailable, r.OiUnverifiable, coverage);
    }

    /// <summary>
    /// Ranks configurations of one side and symbol without looking at the test period.
    /// Eligible = enough trades in training and validation. Validated = profit factor above 1 and net profit above 0 in BOTH
    /// training and validation. Rank = validated first, then validation net profit, profit factor, and lower drawdown.
    /// </summary>
    public static List<GridRow> Rank(IEnumerable<GridRow> rows, int minTradesPerPeriod)
    {
        bool Eligible(GridRow r) => r.Train.TotalTrades >= minTradesPerPeriod && r.Validation.TotalTrades >= minTradesPerPeriod;
        return rows.Where(Eligible)
            .OrderByDescending(IsValidated)
            .ThenByDescending(r => r.Validation.NetProfit)
            .ThenByDescending(r => r.Validation.ProfitFactor ?? 0)
            .ThenBy(r => r.Validation.MaxDrawdown)
            .ToList();
    }

    public static bool IsValidated(GridRow r) =>
        r.Train.NetProfit > 0 && r.Validation.NetProfit > 0 && r.Train.ProfitFactor > 1 && r.Validation.ProfitFactor > 1;
}

/// <summary>Per-symbol facts the exit model needs, measured from real signals and instruments (nothing invented).</summary>
public sealed record ExitModelInputs(
    IReadOnlyDictionary<string, double> AvgDelta,
    IReadOnlyDictionary<string, double> AvgPremium,
    IReadOnlyDictionary<string, int> LotSizes);
