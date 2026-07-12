using Trading.Application.Interfaces;
using Trading.Infrastructure.Data;

namespace Trading.Infrastructure.Repositories;

public class UnitOfWork(TradingDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}
