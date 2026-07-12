using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

public class PerformanceAnalyticsService
{
    public decimal WinRate(int wins, int losses) =>
        wins + losses == 0 ? 0 : (decimal)wins / (wins + losses) * 100;

    public decimal ProfitFactor(decimal grossProfit, decimal grossLoss) =>
        grossLoss == 0 ? grossProfit : grossProfit / grossLoss;

    public decimal MaxDrawdown(IEnumerable<decimal> equityCurve)
    {
        decimal peak = decimal.MinValue;
        decimal maxDd = 0;

        foreach (var equity in equityCurve)
        {
            if (equity > peak) peak = equity;
            var dd = peak == 0 ? 0 : (peak - equity) / peak * 100;
            if (dd > maxDd) maxDd = dd;
        }

        return maxDd;
    }

    // Annualised Sharpe ratio assuming daily returns and 252 trading days.
    public decimal SharpeRatio(IEnumerable<decimal> dailyReturns, decimal riskFreeRate = 0.065m)
    {
        var returns = dailyReturns.ToList();
        if (returns.Count < 2) return 0;

        var dailyRfr = riskFreeRate / 252;
        var excess = returns.Select(r => r - dailyRfr).ToList();
        var avg = excess.Average();
        var variance = excess.Sum(r => (r - avg) * (r - avg)) / (excess.Count - 1);
        var stdDev = (decimal)Math.Sqrt((double)variance);

        return stdDev == 0 ? 0 : avg / stdDev * (decimal)Math.Sqrt(252);
    }

    public decimal AverageWin(IEnumerable<TradeJournal> trades) =>
        trades.Where(t => t.ProfitLoss > 0)
              .Select(t => t.ProfitLoss) is { } wins && wins.Any()
              ? wins.Average() : 0;

    public decimal AverageLoss(IEnumerable<TradeJournal> trades) =>
        trades.Where(t => t.ProfitLoss < 0)
              .Select(t => t.ProfitLoss) is { } losses && losses.Any()
              ? Math.Abs(losses.Average()) : 0;

    public decimal ExpectancyPerTrade(IEnumerable<TradeJournal> trades)
    {
        var list = trades.ToList();
        if (list.Count == 0) return 0;

        int wins = list.Count(t => t.ProfitLoss > 0);
        int losses = list.Count(t => t.ProfitLoss < 0);
        var winRate = WinRate(wins, losses) / 100m;
        var avgWin = AverageWin(list);
        var avgLoss = AverageLoss(list);

        return (winRate * avgWin) - ((1 - winRate) * avgLoss);
    }

    public PerformanceSummary BuildSummary(IEnumerable<TradeJournal> trades, IEnumerable<DailyPnL> dailyPnl)
    {
        var tradeList = trades.ToList();
        var wins = tradeList.Count(t => t.ProfitLoss > 0);
        var losses = tradeList.Count(t => t.ProfitLoss < 0);
        var grossProfit = tradeList.Where(t => t.ProfitLoss > 0).Sum(t => t.ProfitLoss);
        var grossLoss = Math.Abs(tradeList.Where(t => t.ProfitLoss < 0).Sum(t => t.ProfitLoss));
        var equityCurve = dailyPnl.OrderBy(d => d.TradeDate)
                                  .Select(d => d.NetProfit)
                                  .ToList();

        return new PerformanceSummary
        {
            TotalTrades = tradeList.Count,
            WinRate = WinRate(wins, losses),
            ProfitFactor = ProfitFactor(grossProfit, grossLoss),
            MaxDrawdown = MaxDrawdown(equityCurve),
            GrossProfit = grossProfit,
            GrossLoss = grossLoss,
            NetProfit = grossProfit - grossLoss,
            AverageWin = AverageWin(tradeList),
            AverageLoss = AverageLoss(tradeList),
            ExpectancyPerTrade = ExpectancyPerTrade(tradeList)
        };
    }
}

public class PerformanceSummary
{
    public int TotalTrades { get; set; }
    public decimal WinRate { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal GrossProfit { get; set; }
    public decimal GrossLoss { get; set; }
    public decimal NetProfit { get; set; }
    public decimal AverageWin { get; set; }
    public decimal AverageLoss { get; set; }
    public decimal ExpectancyPerTrade { get; set; }
}
