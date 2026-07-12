namespace Trading.SignalEngine.Services;

/// <summary>
/// Position sizing and risk checks.
///
/// Example:
/// Capital      = 100000
/// RiskPercent  = 1
/// RiskBudget   = 1000
///
/// Entry        = 250
/// StopLoss     = 200
/// StopDistance = 50
///
/// LotSize      = 75
/// RiskPerLot   = 50 * 75 = 3750
///
/// Lots         = floor(1000 / 3750) = 0
///
/// CanTrade     = false
/// </summary>
public class RiskManagementService
{
    /// <summary>
    /// Returns true only if at least one lot
    /// fits within the configured risk budget.
    /// </summary>
    public bool CanTrade(
        decimal capital,
        decimal riskPercent,
        decimal entryPrice,
        decimal stopLossPrice,
        int lotSize = 50)
    {
        if (capital <= 0 ||
            riskPercent <= 0 ||
            entryPrice <= 0 ||
            stopLossPrice <= 0 ||
            lotSize <= 0)
        {
            return false;
        }

        var stopDistance =
            Math.Abs(entryPrice - stopLossPrice);

        if (stopDistance <= 0)
            return false;

        var riskBudget =
            capital * riskPercent / 100m;

        var riskPerLot =
            stopDistance * lotSize;

        return riskBudget >= riskPerLot;
    }

    /// <summary>
    /// Returns number of lots allowed by risk budget.
    /// Returns 0 when trade exceeds risk budget.
    /// </summary>
    public int GetLotCount(
        decimal capital,
        decimal riskPercent,
        decimal entryPrice,
        decimal stopLossPrice,
        int lotSize = 50)
    {
        if (capital <= 0 ||
            riskPercent <= 0 ||
            entryPrice <= 0 ||
            stopLossPrice <= 0 ||
            lotSize <= 0)
        {
            return 0;
        }

        var stopDistance =
            Math.Abs(entryPrice - stopLossPrice);

        if (stopDistance <= 0)
            return 0;

        var riskBudget =
            capital * riskPercent / 100m;

        var riskPerLot =
            stopDistance * lotSize;

        if (riskPerLot <= 0)
            return 0;

        var lots =
            (int)Math.Floor(
                riskBudget / riskPerLot);

        return Math.Max(0, lots);
    }

    /// <summary>
    /// Returns actual rupee risk for one lot.
    /// Useful for logging and dashboards.
    /// </summary>
    public decimal GetRiskPerLot(
        decimal entryPrice,
        decimal stopLossPrice,
        int lotSize = 50)
    {
        if (entryPrice <= 0 ||
            stopLossPrice <= 0 ||
            lotSize <= 0)
        {
            return 0;
        }

        var stopDistance =
            Math.Abs(entryPrice - stopLossPrice);

        return stopDistance * lotSize;
    }

    /// <summary>
    /// Returns maximum allowed risk amount in rupees.
    /// </summary>
    public decimal GetRiskBudget(
        decimal capital,
        decimal riskPercent)
    {
        if (capital <= 0 || riskPercent <= 0)
            return 0;

        return capital * riskPercent / 100m;
    }

    /// <summary>
    /// Returns total exposure for selected lots.
    /// Useful for broker margin validation.
    /// </summary>
    public decimal GetTotalRisk(
        decimal entryPrice,
        decimal stopLossPrice,
        int lotSize,
        int lots)
    {
        if (lots <= 0)
            return 0;

        return GetRiskPerLot(
                   entryPrice,
                   stopLossPrice,
                   lotSize)
               * lots;
    }
}