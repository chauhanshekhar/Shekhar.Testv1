using Trading.Backtest.Simulation;
using Trading.Domain;

namespace Trading.Backtest.Reporting;

/// <summary>
/// Every metric uses one definition, and an undefined value is null (never 0, never infinity):
///   trade        = one position, partial exits folded in (like TradeJournal aggregated by SignalId);
///   win / loss   = trade P&amp;L above +0.5 / below -0.5 rupees; anything between is a break-even trade;
///   WinRatePct   = wins / trades (break-even trades stay in the denominator); null with no trades;
///   ProfitFactor = gross profit / gross loss; null when there is no loss;
///   MaxDrawdown  = largest peak-to-trough fall of cumulative P&amp;L in exit-time order (0 when it never falls);
///   StopLossCount     = exits at the initial stop (OPTION_SL_HIT, INDEX_SL_HIT);
///   BreakevenStopCount= exits at a protected or trailed stop (PROTECTED_SL_HIT, TRAILING_SL_HIT);
///   Target3RCount     = trades that reached the 3R ladder level (mid sale or full exit);
///   Target5RCount     = trades that exited at the 5R final target;
///   NetProfitAfterCosts = null unless a cost model was supplied (the database holds no cost columns).
/// </summary>
public sealed record PerformanceMetrics(
    int TotalTrades, int WinningTrades, int LosingTrades, int BreakEvenTrades,
    int StopLossCount, int BreakevenStopCount, int Target3RCount, int Target5RCount,
    double? WinRatePct, double GrossProfit, double GrossLoss, double NetProfit, double? NetProfitAfterCosts,
    double? ProfitFactor, double MaxDrawdown, double? AverageProfitPerTrade, double? AverageWin, double? AverageLoss)
{
    private const double FlatBand = 0.5;

    public static PerformanceMetrics Compute(IEnumerable<Trade> trades)
    {
        var list = trades.OrderBy(t => t.ExitUtc).ToList();
        var wins = list.Where(t => t.PnlRs > FlatBand).ToList();
        var losses = list.Where(t => t.PnlRs < -FlatBand).ToList();
        var gp = wins.Sum(t => t.PnlRs);
        var gl = -losses.Sum(t => t.PnlRs);

        double equity = 0, peak = 0, dd = 0;
        foreach (var t in list)
        {
            equity += t.PnlRs;
            peak = Math.Max(peak, equity);
            dd = Math.Max(dd, peak - equity);
        }

        double? afterCosts = list.Count > 0 && list.All(t => t.CostRs is not null) ? list.Sum(t => t.NetRs!.Value) : null;

        return new PerformanceMetrics(
            list.Count, wins.Count, losses.Count, list.Count - wins.Count - losses.Count,
            list.Count(t => t.ExitReason is TradingConst.ExitReason.OptionSlHit or TradingConst.ExitReason.IndexSlHit),
            list.Count(t => t.ExitReason is "PROTECTED_SL_HIT" or TradingConst.ExitReason.TrailingSlHit),
            list.Count(t => t.ReachedMid), list.Count(t => t.ExitReason == TradingConst.ExitReason.OptionT5Hit),
            list.Count > 0 ? 100.0 * wins.Count / list.Count : null,
            gp, gl, gp - gl, afterCosts,
            gl > 0 ? gp / gl : null,
            dd,
            list.Count > 0 ? (gp - gl) / list.Count : null,
            wins.Count > 0 ? gp / wins.Count : null,
            losses.Count > 0 ? -gl / losses.Count : null);
    }
}

/// <summary>Chronological split of the session dates into training, validation and a final out-of-sample test period.</summary>
public sealed record PeriodSplit(DateOnly[] Train, DateOnly[] Validation, DateOnly[] Test)
{
    /// <param name="trainShare">fraction of sessions used for training (default 0.5)</param>
    /// <param name="validationShare">fraction for validation (default 0.25); the remainder is the untouched test period</param>
    public static PeriodSplit Create(IEnumerable<DateOnly> sessions, double trainShare = 0.5, double validationShare = 0.25)
    {
        var all = sessions.Distinct().OrderBy(d => d).ToArray();
        var nTrain = (int)Math.Round(all.Length * trainShare);
        var nVal = (int)Math.Round(all.Length * validationShare);
        return new PeriodSplit(all[..nTrain], all[nTrain..(nTrain + nVal)], all[(nTrain + nVal)..]);
    }

    public IEnumerable<Trade> InTrain(IEnumerable<Trade> t) => t.Where(x => Train.Contains(x.TradeDate));
    public IEnumerable<Trade> InValidation(IEnumerable<Trade> t) => t.Where(x => Validation.Contains(x.TradeDate));
    public IEnumerable<Trade> InTest(IEnumerable<Trade> t) => t.Where(x => Test.Contains(x.TradeDate));
}
