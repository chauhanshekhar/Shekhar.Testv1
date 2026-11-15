# Fix proposal: exit logic reads a median of the last 3 index ticks (NOT APPLIED — awaiting approval)

## Problem
`ExitWorker` takes the index price from the latest `MarketTicks` row: `ExitDataLoader.LoadLatestTicksAsync` →
`MarketTickQueries.LatestPricesAsync`, which is TOP(1) per symbol. That one row drives several checks in
`ExitDecisionService`:
- index T1 (arms `ProtectedSL`)
- index SL
- trailing logic

Sometimes the feed prints a single tick equal to the previous close. Over 10-22..10-28 there were 43 one-tick spikes
(> 0.15% away from both neighbouring ticks), 26 of them exactly equal to `YesterdayClose`. 4 of 70 trades had an exit
or a state change within 2 s of one of these spikes. Example: 10287 (BANKNIFTY CE, 10-26 13:20:10) read
54287.35 against a market around 54130. That set `IndexT1Hit` and `ProtectedSL` = entry × 0.97, and the trade
exited 9 s after entry at -1,602.

## Root cause
The exit path accepts a single tick with no confirmation. The fallback print probably comes from the Groww quote
mapping returning the close when LTP is missing. That is a separate investigation (the same symptom shows up as
`IndicatorSnapshots.DayOpen` drifting during the day) and this fix does not cover it.

## Minimal fix
Exit path only: read up to the last 3 ticks per symbol in the same 30 s window and use their **median**.
- It is still one filtered TOP(N) index seek per symbol, so the 10-07 deadlock fix is kept.
- With fewer than 3 ticks, use the latest tick, which is today's behaviour.
- `LatestPricesAsync` itself is unchanged, so DailyPnlWorker, PaperTradingController and OptionChainController are not affected.

### Diff
`src/Trading.Infrastructure/Repositories/MarketTickQueries.cs`, new method added after `LatestPricesAsync`:
```csharp
    /// <summary>
    /// Median of the last <paramref name="count"/> LastPrice values per symbol at or after
    /// <paramref name="cutoff"/> (one TOP(N) seek per symbol). With fewer than <paramref name="count"/>
    /// ticks the latest tick is used. Rejects a single bad print: on 2026-10-26 a lone BANKNIFTY tick equal
    /// to the previous close armed index-T1 protection and closed signal 10287 nine seconds after entry.
    /// </summary>
    public static async Task<Dictionary<string, decimal>> MedianRecentPricesAsync(
        this IRepository<MarketTick> tickRepo,
        IEnumerable<string?> symbols,
        DateTime cutoff,
        int count = 3)
    {
        var ltpBySymbol = new Dictionary<string, decimal>();

        foreach (var symbol in symbols.Where(s => !string.IsNullOrEmpty(s)).Distinct())
        {
            var recent = await tickRepo.FindAsync(
                t => t.Symbol == symbol && t.TickTime >= cutoff,
                q => q.OrderByDescending(t => t.TickTime).Take(count));

            if (recent.Count == 0) continue;

            ltpBySymbol[symbol!] = recent.Count < count
                ? recent[0].LastPrice
                : recent.Select(t => t.LastPrice).OrderBy(p => p).ElementAt(count / 2);
        }

        return ltpBySymbol;
    }
```
`src/Trading.Worker/Exits/ExitDataLoader.cs`:
```diff
-    // One TOP(1) seek per symbol (see MarketTickQueries). The previous TickTime-only filter scanned
+    // One TOP(3) seek per symbol, median price (see MarketTickQueries): a single bad print (feed
+    // fallback to the previous close) must not arm index-T1 protection or hit the index SL. The previous TickTime-only filter scanned
     ...
-        => tickRepo.LatestPricesAsync(symbols, IstClock.Now.AddSeconds(-30));
+        => tickRepo.MedianRecentPricesAsync(symbols, IstClock.Now.AddSeconds(-30));
```
`src/Trading.Tests/ExitDataLoaderTickTests.cs`: the existing tests stay and still pass, because each has ≤ 2 ticks
per symbol. The `..._IsOneFilteredTop1Query_...` name and comment change to TopN; the assertions are unchanged.
New tests:
- `SingleSpikeTick_IsIgnored`: 54137, 54138, then 54287 → 54138.
- `SustainedMove_IsFollowedWithinOneTick`: 54100, 54250, 54252 → 54250.
- `FewerThanThreeTicks_UsesLatest`.

## Evidence (`median3_check.py`, 334,092 ticks, 10-22..10-28)
- Median of the last 3 removes **41 of 43** spikes.
  - The 2 that get through are two consecutive bad prints at 15:20-15:21 on 10-22, inside the force-exit window.
- 54 of the 56 large jumps in the median are real persistent moves, and these pass through.
- Tracking error against the raw tick: median 0.0009%, p99 0.017%. Real moves lag by about one tick (~1 s).

## Risk
- **The index stop and index T1 react about 1 s later on a real move.** The option-side checks (`OptionSL` from live
  option quotes) and the broker-resting `SL_M` (`BrokerStopCovers`) do not use this price, so capital-protection
  stops are not delayed.
- **Two consecutive bad ticks still get through.** That is 2 cases in 7 days, both in the force-exit window.
- **No change to:** schema, settings, entry logic, duplicate-order protection, TradeJournal, or BrokerOrderSyncWorker.
- **DB load:** each seek is TOP(3) instead of TOP(1) on the same `IX_MarketTick_Symbol_TickTime` index, which is negligible.

## Validation
- `dotnet build TradingPlatform.slnx`, then `dotnet test src/Trading.Tests` (existing tests plus the 3 new ones).
- After deploy, in paper mode, check that no PROTECTED_SL_HIT or INDEX_SL_HIT exit happens within 2 s of a spike in
  `tick-spikes.csv` (re-run `bad_ticks.py` and `spike_hits.py` after about 5 sessions).

## Deployment impact
Trading.Worker rebuild and redeploy only. No migration and no settings change. Rollback = revert the 3 files.
