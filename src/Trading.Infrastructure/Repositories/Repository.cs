using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Trading.Infrastructure.Data;

namespace Trading.Infrastructure.Repositories;

public class Repository<T> : IRepository<T>
    where T : class
{
    private readonly TradingDbContext _db;

    public Repository(TradingDbContext db)
    {
        _db = db;
    }

    // ── Basic ─────────────────────────────────────────────────────────────

    public async Task<List<T>> GetAllAsync()
        => await _db.Set<T>().ToListAsync();

    public async Task<T?> GetByIdAsync(int id)
        => await _db.Set<T>().FindAsync(id);

    // ── Filtered queries (EF-translated → SQL, never loads full table) ────

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
        => await _db.Set<T>().Where(predicate).ToListAsync();

    public async Task<List<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>> queryModifier)
        => await queryModifier(_db.Set<T>().Where(predicate)).ToListAsync();

    public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        => await _db.Set<T>().FirstOrDefaultAsync(predicate);

    // ── Mutations ─────────────────────────────────────────────────────────
    // Note: these call SaveChangesAsync for backwards compatibility.
    // Callers that manage their own UoW should use the DbContext directly.

    public async Task AddAsync(T entity)
    {
        await _db.Set<T>().AddAsync(entity);
        await _db.SaveChangesAsync();
    }

    public async Task AddRangeAsync(IEnumerable<T> entities)
    {
        await _db.Set<T>().AddRangeAsync(entities);
        await _db.SaveChangesAsync();
    }

    public async Task StageAsync(T entity)
        => await _db.Set<T>().AddAsync(entity);

    public async Task StageRangeAsync(IEnumerable<T> entities)
        => await _db.Set<T>().AddRangeAsync(entities);

    public async Task UpdateAsync(T entity)
    {
        _db.Set<T>().Update(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(T entity)
    {
        _db.Set<T>().Remove(entity);
        await _db.SaveChangesAsync();
    }

    public async Task DeleteRangeAsync(IEnumerable<T> entities)
    {
        _db.Set<T>().RemoveRange(entities);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Translates directly to: DELETE FROM [table] WHERE [predicate]
    /// No rows are loaded into the change tracker — O(1) memory regardless of row count.
    /// Use this instead of FindAsync + DeleteRangeAsync for large bulk deletes.
    /// </summary>
    public async Task<int> ExecuteDeleteAsync(Expression<Func<T, bool>> predicate)
        => await _db.Set<T>().Where(predicate).ExecuteDeleteAsync();

    public async Task<int> CountAsync(Expression<Func<T, bool>> predicate)
        => await _db.Set<T>().CountAsync(predicate);
}
