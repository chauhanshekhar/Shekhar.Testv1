using System.Globalization;
using System.Text;

namespace Trading.Backtest.Reporting;

/// <summary>Writes the comparison grid as CSV (every metric, NULL when undefined) and a ranked Markdown summary.</summary>
public static class GridWriter
{
    private static readonly string[] Header =
    [
        "Configuration", "Symbol", "TradeType", "TotalSignals", "TotalTrades", "WinningTrades", "LosingTrades", "BreakEvenTrades",
        "StopLossCount", "BreakevenStopCount", "Target3RCount", "Target5RCount", "WinRatePct", "GrossProfit", "GrossLoss",
        "NetProfit", "NetProfitAfterCosts", "ProfitFactor", "MaxDrawdown", "AverageProfitPerTrade", "AverageWin", "AverageLoss",
        "BlockedCount",
        "TrainTrades", "TrainNetProfit", "TrainProfitFactor", "TrainMaxDrawdown",
        "ValidationTrades", "ValidationNetProfit", "ValidationProfitFactor", "ValidationMaxDrawdown", "ValidationWinRatePct",
        "TestTrades", "TestNetProfit", "TestProfitFactor", "TestMaxDrawdown",
        "Validated", "RealPremiumTrades", "DataUnavailable", "OiUnverifiable", "BlockedDetail", "Coverage",
    ];

    public static string F(double? v) => v is null || double.IsNaN(v.Value) || double.IsInfinity(v.Value)
        ? "NULL" : v.Value.ToString("0.##", CultureInfo.InvariantCulture);

    public static void WriteCsv(string path, IEnumerable<GridRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", Header));
        foreach (var r in rows)
        {
            var a = r.All;
            var cells = new[]
            {
                Q(r.Configuration), r.Symbol, r.TradeType, r.TotalSignals.ToString(), a.TotalTrades.ToString(),
                a.WinningTrades.ToString(), a.LosingTrades.ToString(), a.BreakEvenTrades.ToString(),
                a.StopLossCount.ToString(), a.BreakevenStopCount.ToString(), a.Target3RCount.ToString(), a.Target5RCount.ToString(),
                F(a.WinRatePct), F(a.GrossProfit), F(a.GrossLoss), F(a.NetProfit), F(a.NetProfitAfterCosts), F(a.ProfitFactor),
                F(a.MaxDrawdown), F(a.AverageProfitPerTrade), F(a.AverageWin), F(a.AverageLoss), r.BlockedCount.ToString(),
                r.Train.TotalTrades.ToString(), F(r.Train.NetProfit), F(r.Train.ProfitFactor), F(r.Train.MaxDrawdown),
                r.Validation.TotalTrades.ToString(), F(r.Validation.NetProfit), F(r.Validation.ProfitFactor), F(r.Validation.MaxDrawdown), F(r.Validation.WinRatePct),
                r.Test.TotalTrades.ToString(), F(r.Test.NetProfit), F(r.Test.ProfitFactor), F(r.Test.MaxDrawdown),
                GridRunner.IsValidated(r) ? "YES" : "NO", r.RealPremiumTrades.ToString(), r.DataUnavailable.ToString(), r.OiUnverifiable.ToString(),
                Q(r.BlockedDetail), Q(r.Coverage),
            };
            sb.AppendLine(string.Join(",", cells));
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    public static void WriteRanking(string path, IReadOnlyList<GridRow> rows, int minTradesPerPeriod, string assumptions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Backtest ranking (training and validation only; the test column was never used to rank)");
        sb.AppendLine();
        sb.AppendLine(assumptions);
        sb.AppendLine();

        foreach (var side in rows.Select(r => r.TradeType).Distinct())
        foreach (var symbol in rows.Where(r => r.TradeType == side).Select(r => r.Symbol).Distinct())
        {
            var subset = rows.Where(r => r.TradeType == side && r.Symbol == symbol).ToList();
            var ranked = GridRunner.Rank(subset, minTradesPerPeriod);
            var baseline = subset.FirstOrDefault(r => r.Configuration.EndsWith("|Baseline", StringComparison.Ordinal));

            sb.AppendLine($"## {side} {symbol}");
            sb.AppendLine();
            if (baseline is not null)
                sb.AppendLine($"Baseline: trades {baseline.All.TotalTrades}, net {F(baseline.All.NetProfit)}, PF {F(baseline.All.ProfitFactor)}, " +
                              $"validation net {F(baseline.Validation.NetProfit)} (PF {F(baseline.Validation.ProfitFactor)}, {baseline.Validation.TotalTrades} trades), " +
                              $"test net {F(baseline.Test.NetProfit)} (PF {F(baseline.Test.ProfitFactor)}, {baseline.Test.TotalTrades} trades).");
            var best = ranked.FirstOrDefault(GridRunner.IsValidated);
            sb.AppendLine(best is null
                ? $"**No configuration is validated** (profit factor above 1 and net above 0 in both training and validation with at least {minTradesPerPeriod} trades in each). {ranked.Count} of {subset.Count} configurations had enough trades."
                : $"**Best validated: {best.Configuration}** - validation net {F(best.Validation.NetProfit)}, PF {F(best.Validation.ProfitFactor)}; held-out test net {F(best.Test.NetProfit)}, PF {F(best.Test.ProfitFactor)} on {best.Test.TotalTrades} trades.");
            sb.AppendLine();
            sb.AppendLine("| Rank | Configuration | Validated | Train trades | Train net | Train PF | Val trades | Val net | Val PF | Val max DD | Test trades | Test net | Test PF |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|");
            var rank = 0;
            foreach (var r in ranked.Take(10))
                sb.AppendLine($"| {++rank} | {r.Configuration} | {(GridRunner.IsValidated(r) ? "YES" : "no")} | {r.Train.TotalTrades} | {F(r.Train.NetProfit)} | {F(r.Train.ProfitFactor)} | " +
                              $"{r.Validation.TotalTrades} | {F(r.Validation.NetProfit)} | {F(r.Validation.ProfitFactor)} | {F(r.Validation.MaxDrawdown)} | " +
                              $"{r.Test.TotalTrades} | {F(r.Test.NetProfit)} | {F(r.Test.ProfitFactor)} |");
            sb.AppendLine();
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    private static string Q(string s) => s.Contains(',') || s.Contains('"') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;
}
