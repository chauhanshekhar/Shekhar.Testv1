using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

public class BacktestService
{
    private readonly EmaService _ema;

    public BacktestService(EmaService ema)
    {
        _ema = ema;
    }

    /// <summary>
    /// Simulates the live strategy on historical candles.
    /// Matches the live logic: EMA20/EMA50 crossover for both BUY and SELL.
    /// Exit: first candle that hits SL or Target1 (same fixed-point rules as SignalEngineService).
    /// If neither is hit, exits at end of session (last candle in data).
    /// </summary>
    public BacktestResult Run(List<Candle> candles, string symbol = "NIFTY")
    {
        int wins = 0, losses = 0;
        decimal grossProfit = 0, grossLoss = 0;
        decimal netProfit = 0, equity = 0, maxEquity = 0, maxDrawdown = 0;

        decimal slPoints = symbol is "BANKNIFTY" or "SENSEX" ? 100 : 50;
        decimal tgt1Pts = slPoints * 2;

        // Need at least 51 candles before the first signal, +1 for exit simulation
        for (int i = 51; i < candles.Count - 1; i++)
        {
            var closes = candles.Take(i).Select(x => x.Close).ToList();

            var ema20 = _ema.GetEma(closes, 20);
            var ema50 = _ema.GetEma(closes, 50);

            var current = candles[i];

            bool buySignal = current.Close > ema20 && ema20 > ema50;
            bool sellSignal = current.Close < ema20 && ema20 < ema50;

            if (!buySignal && !sellSignal) continue;

            var entry = current.Close;
            var sl = buySignal ? entry - slPoints : entry + slPoints;
            var target = buySignal ? entry + tgt1Pts : entry - tgt1Pts;

            // Scan forward candles until SL or Target is hit (or we run out)
            decimal pnl = 0;
            bool hit = false;
            int exitIdx = candles.Count - 1;
            for (int j = i + 1; j < candles.Count; j++)
            {
                var exitCandle = candles[j];

                if (buySignal)
                {
                    if (exitCandle.Low <= sl) { pnl = sl - entry; hit = true; exitIdx = j; break; }
                    if (exitCandle.High >= target) { pnl = target - entry; hit = true; exitIdx = j; break; }
                }
                else
                {
                    if (exitCandle.High >= sl) { pnl = entry - sl; hit = true; exitIdx = j; break; }
                    if (exitCandle.Low <= target) { pnl = entry - target; hit = true; exitIdx = j; break; }
                }
            }

            // End-of-data exit if SL/target never hit
            if (!hit)
                pnl = buySignal
                    ? candles[^1].Close - entry
                    : entry - candles[^1].Close;

            // Advance outer loop past the exit bar to prevent overlapping trades.
            i = exitIdx;

            netProfit += pnl;
            equity += pnl;

            if (pnl > 0) { wins++; grossProfit += pnl; }
            else { losses++; grossLoss += Math.Abs(pnl); }

            if (equity > maxEquity) maxEquity = equity;
            var drawdown = maxEquity - equity;
            if (drawdown > maxDrawdown) maxDrawdown = drawdown;
        }

        int total = wins + losses;
        var winRate = total == 0 ? 0 : (decimal)wins / total * 100;

        // ProfitFactor = total ₹ won / total ₹ lost (industry standard)
        var profitFactor = grossLoss == 0
            ? (grossProfit > 0 ? 99m : 0m)
            : Math.Round(grossProfit / grossLoss, 4);

        return new BacktestResult
        {
            TotalTrades = total,
            WinningTrades = wins,
            LosingTrades = losses,
            WinRate = winRate,
            NetProfit = netProfit,
            MaxDrawdown = maxDrawdown,
            ProfitFactor = profitFactor,
            AveragePnL = total == 0 ? 0 : netProfit / total
        };
    }
}
