using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

public class PortfolioService
{
    public decimal CalculatePnL(Position position) =>
        (position.CurrentPrice - position.AvgPrice) * position.Quantity;

    public decimal TotalEquity(Portfolio portfolio, IEnumerable<Position> positions)
    {
        var unrealisedPnl = positions.Sum(p => CalculatePnL(p));
        return portfolio.Capital + unrealisedPnl;
    }

    public decimal PortfolioUtilisation(Portfolio portfolio) =>
        portfolio.Capital == 0 ? 0 :
        portfolio.UsedMargin / portfolio.Capital * 100;

    public Portfolio UpdatePortfolio(Portfolio portfolio, IEnumerable<Position> positions)
    {
        var unrealisedPnl = positions.Sum(p => CalculatePnL(p));
        portfolio.NetPnL = unrealisedPnl;
        portfolio.AvailableMargin = portfolio.Capital - portfolio.UsedMargin;
        portfolio.UpdatedOn = IstClock.Now;
        return portfolio;
    }

    public IEnumerable<Position> TopGainers(IEnumerable<Position> positions, int count = 5) =>
        positions.OrderByDescending(p => CalculatePnL(p)).Take(count);

    public IEnumerable<Position> TopLosers(IEnumerable<Position> positions, int count = 5) =>
        positions.OrderBy(p => CalculatePnL(p)).Take(count);
}
