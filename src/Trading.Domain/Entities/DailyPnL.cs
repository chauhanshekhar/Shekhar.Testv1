using Trading.Domain;
namespace Trading.Domain.Entities;

public class DailyPnL
{
    public int Id { get; set; }
    public DateTime TradeDate { get; set; }

    // ── Core P&L ──────────────────────────────────────────────────────────────
    public decimal TotalProfit { get; set; }
    public decimal TotalLoss { get; set; }
    public decimal NetProfit { get; set; }
    public int TotalTrades { get; set; }

    // ── Performance metrics ───────────────────────────────────────────────────
    /// <summary>Number of trades that closed in profit.</summary>
    public int WinTrades { get; set; }

    /// <summary>Number of trades that closed at a loss.</summary>
    public int LossTrades { get; set; }

    /// <summary>Number of trades that closed at exactly breakeven (PnL = 0).</summary>
    public int BreakevenTrades { get; set; }

    /// <summary>Win rate as a percentage (0–100).</summary>
    public decimal WinRate { get; set; }

    /// <summary>
    /// Profit factor = GrossProfit / GrossLoss.
    /// Values > 1 mean the strategy made more than it lost.
    /// Stored as 0 when there are no losing trades (perfect day).
    /// </summary>
    public decimal ProfitFactor { get; set; }

    /// <summary>
    /// Largest peak-to-trough drop in running P&L during the day.
    /// Stored as a positive number (e.g. 2500 means ₹2,500 drawdown).
    /// </summary>
    public decimal MaxDrawdown { get; set; }

    /// <summary>Closed-trade net P&L (alias for NetProfit — explicit for kill-switch consumers).</summary>
    public decimal RealizedPnl   { get; set; }

    /// <summary>Open-position MTM P&L at last aggregation tick.</summary>
    public decimal UnrealizedPnl { get; set; }

    /// <summary>
    /// Worst-case intraday loss = max(MaxDrawdown, current open-position loss).
    /// Kill switch compares this against MaxDailyLoss.
    /// </summary>
    public decimal DailyDrawdown { get; set; }

    /// <summary>Timestamp of the last aggregation run (UTC).</summary>
    public DateTime UpdatedOn { get; set; } = IstClock.Now;
}
