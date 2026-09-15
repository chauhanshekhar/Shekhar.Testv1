/*
================================================================================
IndicatorSnapshotOutcomes — analytics
================================================================================
READ-ONLY. SELECT statements only. READ UNCOMMITTED so these can be run during
market hours without taking locks that block the live workers.

All moves are INDEX points measured on IndicatorSnapshot.Ltp.
TARGET = +20 index points in the snapshot's direction, reached FIRST.
SL     = -20 index points against it, reached FIRST.
This is NOT option premium.

Rows where FirstHitResult15Min IS NULL are still pending (their 15-minute window
has not elapsed, or has not been calculated yet) and are excluded from rates —
counting them as NONE would understate every win rate.
================================================================================
*/


/* ============================================================================
   QUERY 1 — headline win rates by Symbol + Direction
   ============================================================================ */

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    o.Symbol,
    o.Direction,
    COUNT(*)                                                            AS Samples,

    -- 5 minute
    SUM(CASE WHEN o.FirstHitResult5Min  = 'TARGET' THEN 1 ELSE 0 END)   AS TargetWins5,
    SUM(CASE WHEN o.FirstHitResult5Min  = 'SL'     THEN 1 ELSE 0 END)   AS SLWins5,
    SUM(CASE WHEN o.FirstHitResult5Min  = 'NONE'   THEN 1 ELSE 0 END)   AS None5,
    CAST(100.0 * SUM(CASE WHEN o.FirstHitResult5Min = 'TARGET' THEN 1 ELSE 0 END)
         / NULLIF(SUM(CASE WHEN o.FirstHitResult5Min IN ('TARGET','SL') THEN 1 ELSE 0 END), 0)
         AS DECIMAL(6,2))                                               AS WinRate5,

    -- 10 minute
    SUM(CASE WHEN o.FirstHitResult10Min = 'TARGET' THEN 1 ELSE 0 END)   AS TargetWins10,
    SUM(CASE WHEN o.FirstHitResult10Min = 'SL'     THEN 1 ELSE 0 END)   AS SLWins10,
    SUM(CASE WHEN o.FirstHitResult10Min = 'NONE'   THEN 1 ELSE 0 END)   AS None10,
    CAST(100.0 * SUM(CASE WHEN o.FirstHitResult10Min = 'TARGET' THEN 1 ELSE 0 END)
         / NULLIF(SUM(CASE WHEN o.FirstHitResult10Min IN ('TARGET','SL') THEN 1 ELSE 0 END), 0)
         AS DECIMAL(6,2))                                               AS WinRate10,

    -- 15 minute
    SUM(CASE WHEN o.FirstHitResult15Min = 'TARGET' THEN 1 ELSE 0 END)   AS TargetWins15,
    SUM(CASE WHEN o.FirstHitResult15Min = 'SL'     THEN 1 ELSE 0 END)   AS SLWins15,
    SUM(CASE WHEN o.FirstHitResult15Min = 'NONE'   THEN 1 ELSE 0 END)   AS None15,
    CAST(100.0 * SUM(CASE WHEN o.FirstHitResult15Min = 'TARGET' THEN 1 ELSE 0 END)
         / NULLIF(SUM(CASE WHEN o.FirstHitResult15Min IN ('TARGET','SL') THEN 1 ELSE 0 END), 0)
         AS DECIMAL(6,2))                                               AS WinRate15,

    CAST(AVG(o.MaxFavorableMove15) AS DECIMAL(10,2))                    AS AvgMaxFavorable15,
    CAST(AVG(o.MaxAdverseMove15)   AS DECIMAL(10,2))                    AS AvgMaxAdverse15
FROM    dbo.IndicatorSnapshotOutcomes AS o
WHERE   o.Symbol IN ('NIFTY', 'BANKNIFTY', 'SENSEX')
  AND   o.FirstHitResult15Min IS NOT NULL      -- exclude still-pending rows
  AND   o.Anomaly IS NULL                      -- exclude untrustworthy paths
GROUP BY o.Symbol, o.Direction
ORDER BY o.Symbol, o.Direction;


/* ============================================================================
   QUERY 2 — condition breakdown, to discover which combinations pay
   ============================================================================
   Joins back to IndicatorSnapshots for the market context that was true AT
   ENTRY, and buckets the continuous indicators so groups are large enough to
   mean something.

   Bucket edges are descriptive only — they are NOT trading rules and no rule
   should be derived from a bucket without a dedicated backtest and its own
   sample-size check. Start from the HAVING clause below (>= 30) and treat
   anything thinner as noise.
   ============================================================================ */

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    o.Symbol,
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

    CASE WHEN s.Confidence <  50 THEN '<50'
         WHEN s.Confidence <  60 THEN '50-60'
         WHEN s.Confidence <  70 THEN '60-70'
         ELSE                         '70+'  END                        AS ConfidenceRange,

    COUNT(*)                                                            AS Samples,
    SUM(CASE WHEN o.FirstHitResult15Min = 'TARGET' THEN 1 ELSE 0 END)   AS Target15,
    SUM(CASE WHEN o.FirstHitResult15Min = 'SL'     THEN 1 ELSE 0 END)   AS SL15,
    SUM(CASE WHEN o.FirstHitResult15Min = 'NONE'   THEN 1 ELSE 0 END)   AS None15,
    CAST(100.0 * SUM(CASE WHEN o.FirstHitResult15Min = 'TARGET' THEN 1 ELSE 0 END)
         / NULLIF(SUM(CASE WHEN o.FirstHitResult15Min IN ('TARGET','SL') THEN 1 ELSE 0 END), 0)
         AS DECIMAL(6,2))                                               AS WinRate15,

    CAST(AVG(o.MaxFavorableMove15) AS DECIMAL(10,2))                    AS AvgMaxFavorable15,
    CAST(AVG(o.MaxAdverseMove15)   AS DECIMAL(10,2))                    AS AvgMaxAdverse15
FROM        dbo.IndicatorSnapshotOutcomes AS o
INNER JOIN  dbo.IndicatorSnapshots        AS s ON s.Id = o.IndicatorSnapshotId
WHERE       o.Symbol IN ('NIFTY', 'BANKNIFTY', 'SENSEX')
  AND       o.FirstHitResult15Min IS NOT NULL
  AND       o.Anomaly IS NULL
GROUP BY
    o.Symbol, o.Direction, s.TrendDirection, s.MarketRegime, s.MarketState,
    CASE WHEN s.Rsi <  30 THEN '00-30' WHEN s.Rsi < 40 THEN '30-40' WHEN s.Rsi < 50 THEN '40-50'
         WHEN s.Rsi <  60 THEN '50-60' WHEN s.Rsi < 70 THEN '60-70' ELSE '70+' END,
    CASE WHEN s.Pcr <  0.80 THEN '<0.80' WHEN s.Pcr < 1.00 THEN '0.80-1.00'
         WHEN s.Pcr <  1.20 THEN '1.00-1.20' ELSE '1.20+' END,
    CASE WHEN s.AtrPct < 0.030 THEN '<0.030' WHEN s.AtrPct < 0.050 THEN '0.030-0.050'
         WHEN s.AtrPct < 0.100 THEN '0.050-0.100' ELSE '0.100+' END,
    CASE WHEN s.Confidence < 50 THEN '<50' WHEN s.Confidence < 60 THEN '50-60'
         WHEN s.Confidence < 70 THEN '60-70' ELSE '70+' END
HAVING COUNT(*) >= 30            -- thin buckets are noise, not edge
ORDER BY WinRate15 DESC, Samples DESC;


/* ============================================================================
   QUERY 3 — progress / health of the tracker itself
   ============================================================================ */

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    CompletedWindowMinutes,
    COUNT(*)                                              AS Rows,
    SUM(CASE WHEN Anomaly IS NOT NULL THEN 1 ELSE 0 END)  AS Anomalies,
    MIN(CreatedOn)                                        AS Oldest,
    MAX(CalculatedOn)                                     AS LastCalculated
FROM   dbo.IndicatorSnapshotOutcomes
GROUP BY CompletedWindowMinutes
ORDER BY CompletedWindowMinutes;

-- Any anomalies recorded, with their snapshot context.
SELECT TOP (100) o.IndicatorSnapshotId, o.Symbol, o.Direction, o.Anomaly, o.CalculatedOn
FROM   dbo.IndicatorSnapshotOutcomes AS o
WHERE  o.Anomaly IS NOT NULL
ORDER BY o.CalculatedOn DESC;
