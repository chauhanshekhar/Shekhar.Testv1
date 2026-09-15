/*
================================================================================
Why SENSEX signals keep hitting stop-loss — evidence gathering
================================================================================
READ-ONLY. SELECT statements only. READ UNCOMMITTED so this can run during
market hours without blocking the live workers.

CODE-VERIFIED CONTEXT (no DB access was available to run this from the
assistant's side — Trusted_Connection to 13.206.229.15 fails from this
machine: "login is from an untrusted domain". Run this yourself and share
the output, or run it from the box that hosts the app.)

1. AtrRiskService.CalculateIndexRisk (src/Trading.SignalEngine/Services/AtrRiskService.cs)
   computes the INITIAL index-level StopLoss for every symbol the same way:

       stopDistance = Clamp(AtrRawAtEntry * AtrStopLossMultiplier,
                             MinimumIndexStopPoints, MaximumIndexStopPoints)

   AtrStopLossMultiplier / MinimumIndexStopPoints (20) / MaximumIndexStopPoints (100)
   come from TradingSettings, bound once from appsettings.json's "Trading" section
   (src/Trading.Infrastructure/Services/SystemSettingsService.cs). They are GLOBAL —
   the exact same 20-100 index-POINT band is applied to NIFTY (~25,000), BANKNIFTY
   (~55,000) and SENSEX (~80,000+). There is no per-symbol override for this clamp
   today (unlike TradingInstruments.IndexStopPoints, which at least splits
   BANKNIFTY/SENSEX=100 vs others=50 for the fallback index-trailing path in
   TrailingStopService — but that's a different code path, not the entry SL).

2. SignalEngineService.cs:248-249 already documents the scale problem in its own
   comment on the CALL-side gates: "20 points is 0.083% [on NIFTY] vs 0.026% on
   SENSEX" — i.e. the SAME point distance is ~3x tighter, in percentage-of-price
   terms, on SENSEX than on NIFTY.

3. StrategySelectorService.cs:39-43 records a real incident: on 2026-08-18, five
   SENSEX signals fired in a Sideways/High-volatility regime and every single one
   hit its stop-loss. That led to suppressing Directional entries in Sideways
   regimes — but does not address whether the SL distance itself is too tight for
   SENSEX's price level in the regimes that ARE still allowed to trade.

HYPOTHESIS UNDER TEST (not yet confirmed with data):
   The 20-100 point clamp is calibrated for NIFTY/BANKNIFTY price levels and is
   proportionately too tight for SENSEX, so ordinary intraday noise (not a real
   reversal) crosses the stop before the anticipated move develops.

This script does NOT assume the hypothesis is correct. Queries 3 and 4 are the
decisive ones — if they show the clamp rarely binds and natural ATR distance
already lands inside a reasonable SENSEX percentage, the real problem is signal
QUALITY (entry timing / condition set), not SL sizing, and query 6 is where to
look instead.
================================================================================
*/


/* ============================================================================
   QUERY 1 — actual exit-reason breakdown, SENSEX vs BANKNIFTY vs NIFTY
   ============================================================================
   Confirms (or refutes) that SENSEX specifically over-indexes on SL exits
   relative to the other two symbols trading through the same engine.
   ============================================================================ */

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    Symbol,
    COUNT(*)                                                              AS ClosedSignals,
    SUM(CASE WHEN ExitReason = 'INDEX_SL_HIT'  THEN 1 ELSE 0 END)         AS IndexSlHits,
    SUM(CASE WHEN ExitReason = 'OPTION_SL_HIT' THEN 1 ELSE 0 END)         AS OptionSlHits,
    SUM(CASE WHEN ExitReason IN ('INDEX_SL_HIT','OPTION_SL_HIT')
         THEN 1 ELSE 0 END)                                               AS TotalSlHits,
    SUM(CASE WHEN ExitReason IN ('OPTION_T2_HIT','PARTIAL_T1')
         THEN 1 ELSE 0 END)                                               AS TargetHits,
    SUM(CASE WHEN ExitReason = 'FORCE_EXIT_EOD' THEN 1 ELSE 0 END)        AS EodExits,
    CAST(100.0 * SUM(CASE WHEN ExitReason IN ('INDEX_SL_HIT','OPTION_SL_HIT') THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*), 0) AS DECIMAL(6,2))                           AS SlHitRatePct
FROM   dbo.Signals
WHERE  Symbol IN ('NIFTY', 'BANKNIFTY', 'SENSEX')
  AND  Status IN ('CLOSED', 'SL_HIT', 'TARGET_HIT')
  AND  ExitReason IS NOT NULL
GROUP  BY Symbol
ORDER  BY SlHitRatePct DESC;


/* ============================================================================
   QUERY 2 — SENSEX SL exits by day/hour, to catch a regime or time-of-day pattern
   ============================================================================ */

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    CAST(CreatedOn AS DATE)              AS TradeDate,
    DATEPART(HOUR, CreatedOn)            AS Hour,
    MarketState,
    COUNT(*)                                                          AS Signals,
    SUM(CASE WHEN ExitReason IN ('INDEX_SL_HIT','OPTION_SL_HIT')
         THEN 1 ELSE 0 END)                                           AS SlHits
FROM   dbo.Signals
WHERE  Symbol = 'SENSEX'
  AND  ExitReason IS NOT NULL
GROUP  BY CAST(CreatedOn AS DATE), DATEPART(HOUR, CreatedOn), MarketState
ORDER  BY TradeDate DESC, Hour;


/* ============================================================================
   QUERY 3 — DECISIVE: is the 20-100 point clamp actually binding for SENSEX?
   ============================================================================
   ActualDistance = |EntryPrice - StopLoss|, taken from the real saved Signal row.
   NaturalDistance = AtrRawAtEntry * 1.5 (AtrStopLossMultiplier), i.e. what the
   formula would have produced BEFORE the Clamp(20,100) in AtrRiskService.

   If ClampedAtMax/ClampedAtMin dominate the row count, the fixed band — not the
   market's actual ATR that day — is setting the stop, and DistancePctOfEntry
   shows how tight that really is relative to SENSEX's price level.
   ============================================================================ */

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    Id,
    CreatedOn,
    SignalType,
    EntryPrice,
    StopLoss,
    ABS(EntryPrice - StopLoss)                                        AS ActualDistance,
    CAST(AtrRawAtEntry * 1.5 AS DECIMAL(10,2))                        AS NaturalDistanceApprox,
    CASE WHEN ABS(EntryPrice - StopLoss) >= 99.5  THEN 'CLAMPED_AT_MAX(100)'
         WHEN ABS(EntryPrice - StopLoss) <= 20.5  THEN 'CLAMPED_AT_MIN(20)'
         ELSE 'NATURAL_ATR' END                                       AS ClampState,
    CAST(100.0 * ABS(EntryPrice - StopLoss) / NULLIF(EntryPrice,0)
         AS DECIMAL(6,4))                                             AS DistancePctOfEntry,
    ExitReason
FROM   dbo.Signals
WHERE  Symbol = 'SENSEX'
  AND  EntryPrice > 0
ORDER  BY CreatedOn DESC;

-- Roll-up of the same thing.
SELECT
    CASE WHEN ABS(EntryPrice - StopLoss) >= 99.5  THEN 'CLAMPED_AT_MAX(100)'
         WHEN ABS(EntryPrice - StopLoss) <= 20.5  THEN 'CLAMPED_AT_MIN(20)'
         ELSE 'NATURAL_ATR' END                                       AS ClampState,
    COUNT(*)                                                          AS Signals,
    CAST(AVG(ABS(EntryPrice - StopLoss)) AS DECIMAL(10,2))            AS AvgDistancePts,
    CAST(AVG(100.0 * ABS(EntryPrice - StopLoss) / NULLIF(EntryPrice,0))
         AS DECIMAL(6,4))                                             AS AvgDistancePctOfEntry,
    SUM(CASE WHEN ExitReason IN ('INDEX_SL_HIT','OPTION_SL_HIT') THEN 1 ELSE 0 END) AS SlHits,
    CAST(100.0 * SUM(CASE WHEN ExitReason IN ('INDEX_SL_HIT','OPTION_SL_HIT') THEN 1 ELSE 0 END)
         / NULLIF(COUNT(*),0) AS DECIMAL(6,2))                        AS SlHitRatePct
FROM   dbo.Signals
WHERE  Symbol = 'SENSEX'
  AND  EntryPrice > 0
GROUP  BY CASE WHEN ABS(EntryPrice - StopLoss) >= 99.5  THEN 'CLAMPED_AT_MAX(100)'
               WHEN ABS(EntryPrice - StopLoss) <= 20.5  THEN 'CLAMPED_AT_MIN(20)'
               ELSE 'NATURAL_ATR' END
ORDER  BY SlHitRatePct DESC;


/* ============================================================================
   QUERY 4 — same DistancePctOfEntry comparison across all three symbols
   ============================================================================
   Puts SENSEX's actual live SL tightness (in % terms) next to NIFTY/BANKNIFTY,
   using the same clamp. A materially smaller AvgDistancePctOfEntry for SENSEX
   is the direct, data-backed confirmation of the hypothesis above.
   ============================================================================ */

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    Symbol,
    COUNT(*)                                                          AS Signals,
    CAST(AVG(ABS(EntryPrice - StopLoss)) AS DECIMAL(10,2))            AS AvgDistancePts,
    CAST(AVG(100.0 * ABS(EntryPrice - StopLoss) / NULLIF(EntryPrice,0))
         AS DECIMAL(6,4))                                             AS AvgDistancePctOfEntry,
    CAST(MIN(100.0 * ABS(EntryPrice - StopLoss) / NULLIF(EntryPrice,0))
         AS DECIMAL(6,4))                                             AS MinDistancePctOfEntry,
    CAST(MAX(100.0 * ABS(EntryPrice - StopLoss) / NULLIF(EntryPrice,0))
         AS DECIMAL(6,4))                                             AS MaxDistancePctOfEntry
FROM   dbo.Signals
WHERE  Symbol IN ('NIFTY','BANKNIFTY','SENSEX')
  AND  EntryPrice > 0
GROUP  BY Symbol
ORDER  BY AvgDistancePctOfEntry;


/* ============================================================================
   QUERY 5 — how fast is SENSEX's stop actually being hit?
   ============================================================================
   Uses IndicatorSnapshotOutcomes (analytics barrier, ±20 INDEX points — NOT the
   live SL, but a proxy for how quickly SENSEX chops through small point moves).
   A short AvgTimeToSLSec relative to AvgTimeToTargetSec is consistent with the
   stop sitting inside normal noise rather than a real adverse move.
   ============================================================================ */

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    o.Direction,
    COUNT(*)                                                              AS Samples,
    CAST(AVG(CASE WHEN o.FirstHitResult15Min = 'SL'
                  THEN CAST(o.SecondsToFirstHit AS DECIMAL(10,2)) END)
         AS DECIMAL(10,1))                                                AS AvgSecondsToSL,
    CAST(AVG(CASE WHEN o.FirstHitResult15Min = 'TARGET'
                  THEN CAST(o.SecondsToFirstHit AS DECIMAL(10,2)) END)
         AS DECIMAL(10,1))                                                AS AvgSecondsToTarget,
    CAST(AVG(o.MaxAdverseMove15) AS DECIMAL(10,2))                        AS AvgMaxAdverse15,
    CAST(AVG(o.MaxFavorableMove15) AS DECIMAL(10,2))                      AS AvgMaxFavorable15
FROM   dbo.IndicatorSnapshotOutcomes AS o
WHERE  o.Symbol = 'SENSEX'
  AND  o.FirstHitResult15Min IS NOT NULL
  AND  o.Anomaly IS NULL
GROUP  BY o.Direction;


/* ============================================================================
   QUERY 6 — best-performing SENSEX condition combinations (signal QUALITY, not
   SL sizing)
   ============================================================================
   Same shape as docs/analyze-indicator-snapshot-outcomes.sql Query 2, scoped to
   SENSEX only. If Query 3/4 show the clamp ISN'T the driver, this is where the
   fix actually lives — which RSI/PCR/ATR/Confidence/MarketState combination has
   a real edge for SENSEX specifically, same evidence-first process already used
   to approve the BANKNIFTY PUT BREAKDOWN rule (SingleSignalRuleEvaluator.cs).

   Bucket edges are descriptive only — treat anything under 30 samples as noise.
   ============================================================================ */

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    o.Direction,
    s.TrendDirection,
    s.MarketRegime,
    s.MarketState,

    CASE WHEN s.Rsi <  30 THEN '00-30'
         WHEN s.Rsi <  40 THEN '30-40'
         WHEN s.Rsi <  50 THEN '40-50'
         WHEN s.Rsi <  60 THEN '50-60'
         WHEN s.Rsi <  70 THEN '60-70'
         ELSE                 '70+'   END                               AS RsiRange,

    CASE WHEN s.Pcr <  0.80 THEN '<0.80'
         WHEN s.Pcr <  1.00 THEN '0.80-1.00'
         WHEN s.Pcr <  1.20 THEN '1.00-1.20'
         ELSE                    '1.20+'  END                           AS PcrRange,

    CASE WHEN s.AtrPct <  0.030 THEN '<0.030'
         WHEN s.AtrPct <  0.050 THEN '0.030-0.050'
         WHEN s.AtrPct <  0.100 THEN '0.050-0.100'
         ELSE                        '0.100+'   END                     AS AtrRange,

    COUNT(*)                                                            AS Samples,
    SUM(CASE WHEN o.FirstHitResult15Min = 'TARGET' THEN 1 ELSE 0 END)   AS Target15,
    SUM(CASE WHEN o.FirstHitResult15Min = 'SL'     THEN 1 ELSE 0 END)   AS SL15,
    CAST(100.0 * SUM(CASE WHEN o.FirstHitResult15Min = 'TARGET' THEN 1 ELSE 0 END)
         / NULLIF(SUM(CASE WHEN o.FirstHitResult15Min IN ('TARGET','SL') THEN 1 ELSE 0 END), 0)
         AS DECIMAL(6,2))                                               AS WinRate15,
    CAST(AVG(o.MaxFavorableMove15) AS DECIMAL(10,2))                    AS AvgMaxFavorable15,
    CAST(AVG(o.MaxAdverseMove15)   AS DECIMAL(10,2))                    AS AvgMaxAdverse15
FROM        dbo.IndicatorSnapshotOutcomes AS o
INNER JOIN  dbo.IndicatorSnapshots        AS s ON s.Id = o.IndicatorSnapshotId
WHERE       o.Symbol = 'SENSEX'
  AND       o.FirstHitResult15Min IS NOT NULL
  AND       o.Anomaly IS NULL
GROUP BY
    o.Direction, s.TrendDirection, s.MarketRegime, s.MarketState,
    CASE WHEN s.Rsi <  30 THEN '00-30' WHEN s.Rsi < 40 THEN '30-40' WHEN s.Rsi < 50 THEN '40-50'
         WHEN s.Rsi <  60 THEN '50-60' WHEN s.Rsi < 70 THEN '60-70' ELSE '70+' END,
    CASE WHEN s.Pcr <  0.80 THEN '<0.80' WHEN s.Pcr < 1.00 THEN '0.80-1.00'
         WHEN s.Pcr <  1.20 THEN '1.00-1.20' ELSE '1.20+' END,
    CASE WHEN s.AtrPct < 0.030 THEN '<0.030' WHEN s.AtrPct < 0.050 THEN '0.030-0.050'
         WHEN s.AtrPct < 0.100 THEN '0.050-0.100' ELSE '0.100+' END
HAVING COUNT(*) >= 30
ORDER BY WinRate15 DESC, Samples DESC;


/*
================================================================================
HOW TO READ THE RESULTS
================================================================================
1. Query 1: confirms whether SENSEX's SL-hit rate is actually worse than
   NIFTY/BANKNIFTY's, or whether it just feels that way.

2. Query 3/4: the core question. If most SENSEX rows are CLAMPED_AT_MIN(20) or
   CLAMPED_AT_MAX(100) rather than NATURAL_ATR, and AvgDistancePctOfEntry for
   SENSEX in Query 4 is visibly smaller than NIFTY/BANKNIFTY's, the fixed
   20-100 point band (TradingSettings.MinimumIndexStopPoints /
   MaximumIndexStopPoints, src/Trading.Infrastructure/TradingSettings.cs:189-191)
   is the root cause: SENSEX's stop sits closer, in real percentage terms, than
   the other two symbols' stops, so ordinary noise reaches it first.

   FIX (do not apply without running this first): make the clamp per-symbol —
   either widen SENSEX's band specifically (e.g. via a new StrategySettings-driven
   override the way MinAtrPercent already is, so it's DB-tunable without a
   rebuild) or switch the clamp to a percentage-of-entry-price band instead of
   fixed index points, which would auto-scale for SENSEX/BANKNIFTY/NIFTY's
   different price levels without needing a per-symbol constant at all. Either
   change touches AtrRiskService.cs (all three engines route through it) — size
   the new bound from THIS query's own AvgDistancePctOfEntry for NIFTY/BANKNIFTY,
   which are presumably performing acceptably, applied to SENSEX's price level.

3. Query 6: if Query 3/4 show the clamp mostly ISN'T binding for SENSEX (most
   rows NATURAL_ATR, percentage distance comparable to the other symbols), the
   problem is signal quality, not stop sizing — pursue a SENSEX-specific
   SingleSignalRuleEvaluator rule the same way BANKNIFTY PUT BREAKDOWN was
   approved: find the (RSI, PCR, ATR, MarketState) combination with a durable
   win rate >= 30 samples per cell, across multiple days, then hand-approve it.

Either path needs the numbers back before anything is changed — per this
project's rule, no production setting or code change should be made from a
hypothesis alone.
================================================================================
*/
