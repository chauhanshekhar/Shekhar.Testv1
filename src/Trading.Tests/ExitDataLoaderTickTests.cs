using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using Trading.Worker.Exits;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// ExitDataLoader reads the latest index tick with one per-symbol TOP(1) seek. On 2026-10-07 the
/// previous TickTime-only query scanned all of MarketTicks twice a cycle and was the deadlock victim
/// 22 times against MarketFeedWorker's per-second tick insert, aborting the exit cycle each time.
/// The seek lives in MarketTickQueries.LatestPricesAsync, also used by DailyPnlWorker and PaperTradingController.
/// </summary>
public class ExitDataLoaderTickTests
{
    [Fact]
    public async Task LatestTickPerSymbol_IsReturned()
    {
        var now = IstClock.Now;
        var ticks = new TickRepo(
            Tick("SENSEX", 72590m, now.AddSeconds(-3)),
            Tick("SENSEX", 72593m, now.AddSeconds(-1)),
            Tick("NIFTY", 25110m, now.AddSeconds(-2)));

        var ltp = await ExitDataLoader.LoadLatestTicksAsync(ticks, ["SENSEX", "NIFTY"]);

        Assert.Equal(72593m, ltp["SENSEX"]);
        Assert.Equal(25110m, ltp["NIFTY"]);
    }

    [Fact]
    public async Task TickOlderThan30Seconds_IsIgnored()
    {
        var ticks = new TickRepo(Tick("BANKNIFTY", 54400m, IstClock.Now.AddSeconds(-45)));

        var ltp = await ExitDataLoader.LoadLatestTicksAsync(ticks, ["BANKNIFTY"]);

        Assert.False(ltp.ContainsKey("BANKNIFTY"));
    }

    [Fact]
    public async Task SymbolWithoutTicks_IsLeftOut_AndOtherSymbolsAreNotReturned()
    {
        var now = IstClock.Now;
        var ticks = new TickRepo(Tick("NIFTY", 25110m, now), Tick("SENSEX", 72593m, now));

        var ltp = await ExitDataLoader.LoadLatestTicksAsync(ticks, ["BANKNIFTY", "NIFTY"]);

        Assert.Equal(["NIFTY"], ltp.Keys);
    }

    [Fact]
    public async Task EverySymbol_IsOneFilteredTopNQuery_NeverATableScan()
    {
        var now = IstClock.Now;
        var ticks = new TickRepo(Tick("SENSEX", 72593m, now), Tick("NIFTY", 25110m, now));

        await ExitDataLoader.LoadLatestTicksAsync(ticks, ["SENSEX", "NIFTY", "SENSEX"]);

        // Duplicates are queried once; each query is the predicate + TOP(N) overload filtered by symbol.
        Assert.Equal(2, ticks.TopQueries.Count);
        Assert.All(ticks.TopQueries, q => Assert.Single(q));
        Assert.Equal(0, ticks.PlainFindCalls);
    }

    [Fact]
    public async Task UnrealizedPnl_KeepsItsSymbolLookups()
    {
        // Paper trade without an OptionSymbol falls back to the index symbol (as before the change).
        var now = IstClock.Now;
        var ticks = new TickRepo(Tick("NIFTY", 110m, now));
        var paper = new ListRepo<PaperTrade>(
            new PaperTrade { Symbol = "NIFTY", OptionSymbol = "", Side = "CALL", EntryPrice = 100m, Quantity = 65, Status = "OPEN" },
            new PaperTrade { Symbol = "SENSEX", OptionSymbol = "SENSEX2610872600PE", Side = "CALL", EntryPrice = 200m, Quantity = 20, Status = "OPEN" });

        var unrealized = await ExitDataLoader.ComputeUnrealizedPnlAsync(paper, ticks);

        Assert.Equal((110m - 100m) * 65, unrealized);   // option symbol has no tick: skipped, as before
        Assert.Equal(0, ticks.PlainFindCalls);
    }

    // ── Single bad print (2026-10-26, signal 10287: one tick = previous close armed index-T1 protection) ──

    [Fact]
    public async Task SingleSpikeTick_IsIgnored()
    {
        var now = IstClock.Now;
        var ticks = new TickRepo(
            Tick("BANKNIFTY", 54137.85m, now.AddSeconds(-2)),
            Tick("BANKNIFTY", 54138.15m, now.AddSeconds(-1)),
            Tick("BANKNIFTY", 54287.35m, now));

        var ltp = await ExitDataLoader.LoadLatestTicksAsync(ticks, ["BANKNIFTY"]);

        Assert.Equal(54138.15m, ltp["BANKNIFTY"]);
    }

    [Fact]
    public async Task SustainedMove_IsFollowedWithinOneTick()
    {
        var now = IstClock.Now;
        var ticks = new TickRepo(
            Tick("BANKNIFTY", 54100m, now.AddSeconds(-2)),
            Tick("BANKNIFTY", 54250m, now.AddSeconds(-1)),
            Tick("BANKNIFTY", 54252m, now));

        var ltp = await ExitDataLoader.LoadLatestTicksAsync(ticks, ["BANKNIFTY"]);

        Assert.Equal(54250m, ltp["BANKNIFTY"]);
    }

    [Fact]
    public async Task FewerThanThreeTicks_UsesLatest()
    {
        var now = IstClock.Now;
        var ticks = new TickRepo(
            Tick("NIFTY", 25100m, now.AddSeconds(-1)),
            Tick("NIFTY", 25140m, now));

        var ltp = await ExitDataLoader.LoadLatestTicksAsync(ticks, ["NIFTY"]);

        Assert.Equal(25140m, ltp["NIFTY"]);
    }

    // ── MarketTickQueries.LatestPricesAsync (shared with DailyPnlWorker and PaperTradingController) ──

    [Fact]
    public async Task LatestPrices_UsesTheCallersWindow()
    {
        // DailyPnlWorker reads a 5-minute window: a 2-minute-old tick counts there, not in the 30 s exit window.
        var now = IstClock.Now;
        var ticks = new TickRepo(Tick("NIFTY", 25110m, now.AddMinutes(-2)));

        Assert.Equal(25110m, (await ticks.LatestPricesAsync(["NIFTY"], now.AddMinutes(-5)))["NIFTY"]);
        Assert.Empty(await ticks.LatestPricesAsync(["NIFTY"], now.AddSeconds(-30)));
    }

    [Fact]
    public async Task LatestPrices_EmptyAndNullSymbols_AreNotQueried()
    {
        // Paper trades carry "" for Leg2OptionSymbol on single-leg trades.
        var ticks = new TickRepo(Tick("SENSEX", 72593m, IstClock.Now));

        var ltp = await ticks.LatestPricesAsync(["", null, "SENSEX"], IstClock.Now.AddSeconds(-60));

        Assert.Equal(72593m, ltp["SENSEX"]);
        Assert.Single(ticks.TopQueries);
        Assert.Equal(0, ticks.PlainFindCalls);
    }

    private static MarketTick Tick(string symbol, decimal price, DateTime at)
        => new() { Symbol = symbol, LastPrice = price, TickTime = at };

    /// <summary>Records each TOP-N query's result; a plain (unshaped) FindAsync is counted so the test can forbid it.</summary>
    private sealed class TickRepo(params MarketTick[] rows) : ListRepo<MarketTick>(rows)
    {
        public readonly List<List<MarketTick>> TopQueries = new();
        public int PlainFindCalls;

        public override Task<List<MarketTick>> FindAsync(Expression<Func<MarketTick, bool>> predicate)
        {
            PlainFindCalls++;
            return base.FindAsync(predicate);
        }

        public override async Task<List<MarketTick>> FindAsync(
            Expression<Func<MarketTick, bool>> predicate, Func<IQueryable<MarketTick>, IQueryable<MarketTick>> queryModifier)
        {
            var result = await base.FindAsync(predicate, queryModifier);
            TopQueries.Add(result);
            return result;
        }
    }

    private class ListRepo<T>(params T[] rows) : IRepository<T> where T : class
    {
        private readonly List<T> _rows = rows.ToList();

        public virtual Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate)
            => Task.FromResult(_rows.AsQueryable().Where(predicate).ToList());

        public virtual Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, Func<IQueryable<T>, IQueryable<T>> queryModifier)
            => Task.FromResult(queryModifier(_rows.AsQueryable().Where(predicate)).ToList());

        public Task<T?> GetByIdAsync(int id) => throw new NotSupportedException();
        public Task<List<T>> GetAllAsync() => throw new NotSupportedException();
        public Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate) => throw new NotSupportedException();
        public Task AddAsync(T entity) => throw new NotSupportedException();
        public Task AddRangeAsync(IEnumerable<T> entities) => throw new NotSupportedException();
        public Task StageAsync(T entity) => throw new NotSupportedException();
        public Task StageRangeAsync(IEnumerable<T> entities) => throw new NotSupportedException();
        public Task UpdateAsync(T entity) => throw new NotSupportedException();
        public Task DeleteAsync(T entity) => throw new NotSupportedException();
        public Task DeleteRangeAsync(IEnumerable<T> entities) => throw new NotSupportedException();
        public Task<int> ExecuteDeleteAsync(Expression<Func<T, bool>> predicate) => throw new NotSupportedException();
        public Task<int> ExecuteUpdateAsync(Expression<Func<T, bool>> predicate, Action<UpdateSettersBuilder<T>> setPropertyCalls)
            => throw new NotSupportedException();
        public Task<int> CountAsync(Expression<Func<T, bool>> predicate) => throw new NotSupportedException();
    }
}
