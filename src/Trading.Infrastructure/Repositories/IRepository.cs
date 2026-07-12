using System.Linq.Expressions;

namespace Trading.Infrastructure.Repositories;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();

    /// <summary>EF-translated WHERE — never loads the full table.</summary>
    Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate);

    /// <summary>EF-translated WHERE + ORDER BY + TAKE — for "top N recent rows".</summary>
    Task<List<T>> FindAsync(
        Expression<Func<T, bool>> predicate,
        Func<IQueryable<T>, IQueryable<T>> queryModifier);

    /// <summary>EF-translated single row lookup — returns null when not found.</summary>
    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

    Task AddAsync(T entity);
    /// <summary>Inserts all entities in one SaveChanges call — far faster than N×AddAsync.</summary>
    Task AddRangeAsync(IEnumerable<T> entities);

    /// <summary>
    /// Tracks the entity for insertion WITHOUT calling SaveChanges.
    /// Use inside a Unit-of-Work block: StageAsync → StageAsync → uow.SaveChangesAsync().
    /// </summary>
    Task StageAsync(T entity);

    /// <summary>
    /// Tracks all entities for insertion WITHOUT calling SaveChanges.
    /// Use inside a Unit-of-Work block: StageRangeAsync → uow.SaveChangesAsync().
    /// </summary>
    Task StageRangeAsync(IEnumerable<T> entities);
    Task UpdateAsync(T entity);
    Task DeleteAsync(T entity);
    /// <summary>Removes all entities in one SaveChanges call.</summary>
    Task DeleteRangeAsync(IEnumerable<T> entities);

    /// <summary>
    /// Issues a single bulk DELETE … WHERE directly to the database.
    /// No rows are loaded into memory — safe for very large deletes (e.g. 72,000 rows).
    /// Requires EF Core 7+.  Returns the number of rows deleted.
    /// </summary>
    Task<int> ExecuteDeleteAsync(Expression<Func<T, bool>> predicate);

    /// <summary>EF-translated COUNT(*) WHERE — no rows loaded into memory.</summary>
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
}
