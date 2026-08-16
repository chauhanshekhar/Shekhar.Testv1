using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

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

    /// <summary>
    /// Issues a single bulk UPDATE … WHERE directly to the database and returns rows affected.
    /// Bypasses the change tracker/identity map entirely, so — unlike a tracked-entity
    /// read-then-save — it always hits the database and sees concurrent writers' committed
    /// changes. Use for atomic conditional claims (e.g. "only proceed if nobody else already
    /// claimed this row"), which a load-then-save can't guarantee across separate DbContext
    /// instances/processes. Requires EF Core 7+ (EF Core 10 changed the setter parameter from
    /// SetPropertyCalls&lt;T&gt; to UpdateSettersBuilder&lt;T&gt;).
    /// </summary>
    Task<int> ExecuteUpdateAsync(
        Expression<Func<T, bool>> predicate,
        Action<UpdateSettersBuilder<T>> setPropertyCalls);

    /// <summary>EF-translated COUNT(*) WHERE — no rows loaded into memory.</summary>
    Task<int> CountAsync(Expression<Func<T, bool>> predicate);
}
