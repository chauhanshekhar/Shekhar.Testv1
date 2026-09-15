/*
================================================================================
 NIFTY — univariate condition screen, CALL and PUT
================================================================================
READ-ONLY.

WHY UNIVARIATE, NOT A CROSS-PRODUCT SWEEP
NIFTY has 14 trading days (data starts 2026-08-14). Daily win-rate variance on
BANKNIFTY implies roughly 20 INDEPENDENT episodes per trading day, so NIFTY
carries on the order of 280 effective observations in total. Splitting that
across a 4-way cross-product guarantees noise. Each feature is therefore tested
ON ITS OWN, over the whole sample, so every cell keeps usable size — and the
number of comparisons stays small enough to reason about.

GEOMETRY — DELIBERATELY UNCLAMPED
Live settings clamp the stop to MinimumIndexStopPoints = 20. NIFTY's average
AtrRaw is 6.23 points, so 1.5 x ATR = 9.35 and the clamp binds on 99.93% of
rows, forcing a 20-point stop against a 12.47-point T1 and an 18.70-point T2 —
risking more than the target is worth. Measuring under that clamp would only
re-measure the clamp. This uses the UNCLAMPED ratio (SL 1.5 x ATR, T1 2 x ATR)
so the question asked is "does any condition predict direction", separate from
"is the live stop size wrong" (it is — see the summary at the end).

THE NULL: for a driftless walk with stop a and target b, P(target first) is
a/(a+b) = 1.5/(1.5+2.0) = 42.86%. Every win rate below must beat that, not 50%.

Both directions are evaluated on EVERY episode — no trend or EMA pre-filter —
so the screen can test whether trend/EMA alignment helps, rather than assuming.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

IF OBJECT_ID('tempdb..#ep')  IS NOT NULL DROP TABLE #ep;
IF OBJECT_ID('tempdb..#out') IS NOT NULL DROP TABLE #out;

-- ── Episodes: first snapshot of each 5-minute slot (DuplicateSignalGate's unit)
SELECT Id, SnapshotTime, Ltp, AtrRaw, AtrPct, Rsi, Rsi5m, Pcr, PriceChangePct,
       Ema20, Ema50, Ema20_5m, Ema50_5m, TrendDirection, MarketState,
       CAST(SnapshotTime AS date) AS TradeDate,
       AtrRaw * 1.5 AS SlDist,
       AtrRaw * 2.0 AS T1Dist
INTO #ep
FROM (
    SELECT s.*,
           ROW_NUMBER() OVER (
               PARTITION BY DATEDIFF(MINUTE,'2000-01-01', s.SnapshotTime)/5
               ORDER BY s.SnapshotTime) AS rn
    FROM dbo.IndicatorSnapshots s
    WHERE s.Symbol = 'NIFTY'
      AND s.SnapshotTime >= '2026-08-14'
      AND CAST(s.SnapshotTime AS time) <= '15:10'
      AND s.Ltp > 0 AND s.AtrRaw > 0 AND s.Ema20 > 0 AND s.Ema50 > 0
) x
WHERE rn = 1;

-- ── First-hit for BOTH directions, one pass over each episode's forward path
-- JOIN + GROUP BY rather than CROSS APPLY: an aggregate may not mix an outer
-- reference with an inner column, which a correlated APPLY would require here.
IF OBJECT_ID('tempdb..#hit') IS NOT NULL DROP TABLE #hit;

SELECT e.Id,
       MIN(CASE WHEN f.Ltp >= e.Ltp + e.T1Dist THEN f.SnapshotTime END) AS CallT1,
       MIN(CASE WHEN f.Ltp <= e.Ltp - e.SlDist THEN f.SnapshotTime END) AS CallSl,
       MIN(CASE WHEN f.Ltp <= e.Ltp - e.T1Dist THEN f.SnapshotTime END) AS PutT1,
       MIN(CASE WHEN f.Ltp >= e.Ltp + e.SlDist THEN f.SnapshotTime END) AS PutSl,
       DATEDIFF(SECOND, e.SnapshotTime, MAX(f.SnapshotTime))            AS PathSec
INTO #hit
FROM #ep e
JOIN dbo.IndicatorSnapshots f
  ON  f.Symbol = 'NIFTY' AND f.Ltp > 0
  AND f.SnapshotTime >  e.SnapshotTime
  AND f.SnapshotTime <= DATEADD(MINUTE, 15, e.SnapshotTime)
GROUP BY e.Id, e.Ltp, e.T1Dist, e.SlDist, e.SnapshotTime;

SELECT e.*,
       h.CallT1, h.CallSl, h.PutT1, h.PutSl, h.PathSec,
       CASE WHEN h.CallT1 IS NOT NULL AND (h.CallSl IS NULL OR h.CallT1 < h.CallSl) THEN  e.T1Dist
            WHEN h.CallSl IS NOT NULL AND (h.CallT1 IS NULL OR h.CallSl < h.CallT1) THEN -e.SlDist
            ELSE 0 END AS CallPts,
       CASE WHEN h.CallT1 IS NOT NULL AND (h.CallSl IS NULL OR h.CallT1 < h.CallSl) THEN 'T1'
            WHEN h.CallSl IS NOT NULL AND (h.CallT1 IS NULL OR h.CallSl < h.CallT1) THEN 'SL'
            ELSE 'OPEN' END AS CallOutcome,
       CASE WHEN h.PutT1 IS NOT NULL AND (h.PutSl IS NULL OR h.PutT1 < h.PutSl) THEN  e.T1Dist
            WHEN h.PutSl IS NOT NULL AND (h.PutT1 IS NULL OR h.PutSl < h.PutT1) THEN -e.SlDist
            ELSE 0 END AS PutPts,
       CASE WHEN h.PutT1 IS NOT NULL AND (h.PutSl IS NULL OR h.PutT1 < h.PutSl) THEN 'T1'
            WHEN h.PutSl IS NOT NULL AND (h.PutT1 IS NULL OR h.PutSl < h.PutT1) THEN 'SL'
            ELSE 'OPEN' END AS PutOutcome
INTO #out
FROM #ep e
JOIN #hit h ON h.Id = e.Id
WHERE h.PathSec >= 13 * 60;   -- window substantially observed

-- ── Univariate screen ───────────────────────────────────────────────────────
;WITH f AS (
    SELECT
        'Rsi1m' AS Feature,
        CASE WHEN Rsi < 35 THEN 'a. <35'  WHEN Rsi < 45 THEN 'b. 35-45'
             WHEN Rsi < 55 THEN 'c. 45-55' WHEN Rsi < 65 THEN 'd. 55-65'
             ELSE 'e. 65+' END AS Bucket, * FROM #out
    UNION ALL SELECT 'Rsi5m',
        CASE WHEN Rsi5m IS NULL THEN 'z. null'
             WHEN Rsi5m < 40 THEN 'a. <40' WHEN Rsi5m < 50 THEN 'b. 40-50'
             WHEN Rsi5m < 60 THEN 'c. 50-60' ELSE 'd. 60+' END, * FROM #out
    UNION ALL SELECT 'AtrPct',
        CASE WHEN AtrPct < 0.025 THEN 'a. <0.025' WHEN AtrPct < 0.030 THEN 'b. 0.025-0.030'
             WHEN AtrPct < 0.035 THEN 'c. 0.030-0.035' ELSE 'd. 0.035+' END, * FROM #out
    UNION ALL SELECT 'PriceChangePct',
        CASE WHEN PriceChangePct <= -0.20 THEN 'a. <=-0.20' WHEN PriceChangePct <= -0.10 THEN 'b. -0.20..-0.10'
             WHEN PriceChangePct <   0.10 THEN 'c. -0.10..0.10' WHEN PriceChangePct < 0.20 THEN 'd. 0.10..0.20'
             ELSE 'e. >=0.20' END, * FROM #out
    UNION ALL SELECT 'TrendDirection', TrendDirection, * FROM #out
    UNION ALL SELECT 'MarketState', CASE WHEN MarketState='' THEN '(blank)' ELSE MarketState END, * FROM #out
    UNION ALL SELECT 'LtpVsEma20', CASE WHEN Ltp > Ema20 THEN 'above' ELSE 'below' END, * FROM #out
    UNION ALL SELECT 'Ema1mAlign', CASE WHEN Ema20 > Ema50 THEN 'bull' ELSE 'bear' END, * FROM #out
    UNION ALL SELECT 'Ema5mAlign', CASE WHEN Ema20_5m > Ema50_5m THEN 'bull' ELSE 'bear' END, * FROM #out
    UNION ALL SELECT 'Pcr',
        CASE WHEN Pcr < 0.8 THEN 'a. <0.8' WHEN Pcr < 0.95 THEN 'b. 0.8-0.95'
             WHEN Pcr < 1.1 THEN 'c. 0.95-1.1' ELSE 'd. 1.1+' END, * FROM #out
    UNION ALL SELECT 'HourOfDay', RIGHT('0'+CAST(DATEPART(HOUR,SnapshotTime) AS varchar(2)),2)+':00', * FROM #out
)
SELECT Feature, Bucket,
    COUNT(*)                  AS Episodes,
    COUNT(DISTINCT TradeDate) AS Days,
    -- CALL
    SUM(CASE WHEN CallOutcome='T1' THEN 1 ELSE 0 END) AS C_T1,
    SUM(CASE WHEN CallOutcome='SL' THEN 1 ELSE 0 END) AS C_SL,
    CAST(SUM(CASE WHEN CallOutcome='T1' THEN 1 ELSE 0 END)*100.0
         / NULLIF(SUM(CASE WHEN CallOutcome IN ('T1','SL') THEN 1 ELSE 0 END),0) AS DECIMAL(6,2)) AS C_Win,
    CAST(AVG(CallPts) AS DECIMAL(8,3)) AS C_ExpPts,
    -- PUT
    SUM(CASE WHEN PutOutcome='T1' THEN 1 ELSE 0 END) AS P_T1,
    SUM(CASE WHEN PutOutcome='SL' THEN 1 ELSE 0 END) AS P_SL,
    CAST(SUM(CASE WHEN PutOutcome='T1' THEN 1 ELSE 0 END)*100.0
         / NULLIF(SUM(CASE WHEN PutOutcome IN ('T1','SL') THEN 1 ELSE 0 END),0) AS DECIMAL(6,2)) AS P_Win,
    CAST(AVG(PutPts) AS DECIMAL(8,3)) AS P_ExpPts
FROM f
GROUP BY Feature, Bucket
HAVING COUNT(*) >= 30
ORDER BY Feature, Bucket;

-- ── Baseline: no condition at all ───────────────────────────────────────────
SELECT 'BASELINE (all episodes)' AS Note,
    COUNT(*) AS Episodes, COUNT(DISTINCT TradeDate) AS Days,
    CAST(SUM(CASE WHEN CallOutcome='T1' THEN 1 ELSE 0 END)*100.0
         / NULLIF(SUM(CASE WHEN CallOutcome IN ('T1','SL') THEN 1 ELSE 0 END),0) AS DECIMAL(6,2)) AS C_Win,
    CAST(AVG(CallPts) AS DECIMAL(8,3)) AS C_ExpPts,
    CAST(SUM(CASE WHEN PutOutcome='T1' THEN 1 ELSE 0 END)*100.0
         / NULLIF(SUM(CASE WHEN PutOutcome IN ('T1','SL') THEN 1 ELSE 0 END),0) AS DECIMAL(6,2)) AS P_Win,
    CAST(AVG(PutPts) AS DECIMAL(8,3)) AS P_ExpPts,
    CAST(42.86 AS DECIMAL(6,2)) AS TheoreticalNull
FROM #out;

/* ============================================================================
   OUT-OF-SAMPLE CHECK — train on the first 10 days, test on the last 4
   ============================================================================
   Any condition found by the screen above must hold in BOTH halves. With 82
   comparisons screened (41 buckets x 2 directions), roughly 4 will look
   significant at p<0.05 by chance alone, so a single good-looking cell means
   nothing on its own.

   NOTE the DISTINCT trap: "SELECT DISTINCT TradeDate, ROW_NUMBER() OVER (...)"
   numbers rows BEFORE dedup, so every row keeps a unique rn and DISTINCT
   removes nothing. Take DISTINCT in a subquery first, then number it.
   ============================================================================ */
WITH d AS (
    SELECT TradeDate, ROW_NUMBER() OVER (ORDER BY TradeDate) AS rn
    FROM (SELECT DISTINCT TradeDate FROM #out) x
),
 t AS (SELECT o.*, CASE WHEN d.rn <= 10 THEN 'TRAIN' ELSE 'TEST' END AS Split
       FROM #out o JOIN d ON d.TradeDate = o.TradeDate)
SELECT Cond, Split, COUNT(*) AS Eps, COUNT(DISTINCT TradeDate) AS Days,
   SUM(T1) AS T1, SUM(SL) AS SL,
   CAST(SUM(T1)*100.0/NULLIF(SUM(T1)+SUM(SL),0) AS DECIMAL(6,2)) AS WinRate,
   CAST(AVG(Pts) AS DECIMAL(8,3)) AS ExpPts
FROM (
   SELECT Split, TradeDate, 'A PUT when PriceChg>=0.20' AS Cond,
          CASE WHEN PutOutcome='T1' THEN 1 ELSE 0 END T1, CASE WHEN PutOutcome='SL' THEN 1 ELSE 0 END SL, PutPts Pts
   FROM t WHERE PriceChangePct >= 0.20
   UNION ALL SELECT Split, TradeDate, 'B PUT when Pcr>=1.1',
          CASE WHEN PutOutcome='T1' THEN 1 ELSE 0 END, CASE WHEN PutOutcome='SL' THEN 1 ELSE 0 END, PutPts
   FROM t WHERE Pcr >= 1.1
   UNION ALL SELECT Split, TradeDate, 'C CALL when Pcr>=1.1',
          CASE WHEN CallOutcome='T1' THEN 1 ELSE 0 END, CASE WHEN CallOutcome='SL' THEN 1 ELSE 0 END, CallPts
   FROM t WHERE Pcr >= 1.1
   UNION ALL SELECT Split, TradeDate, 'D CALL when Ltp<Ema20',
          CASE WHEN CallOutcome='T1' THEN 1 ELSE 0 END, CASE WHEN CallOutcome='SL' THEN 1 ELSE 0 END, CallPts
   FROM t WHERE Ltp < Ema20
   UNION ALL SELECT Split, TradeDate, 'E BASELINE all PUT',
          CASE WHEN PutOutcome='T1' THEN 1 ELSE 0 END, CASE WHEN PutOutcome='SL' THEN 1 ELSE 0 END, PutPts FROM t
   UNION ALL SELECT Split, TradeDate, 'F BASELINE all CALL',
          CASE WHEN CallOutcome='T1' THEN 1 ELSE 0 END, CASE WHEN CallOutcome='SL' THEN 1 ELSE 0 END, CallPts FROM t
) z GROUP BY Cond, Split ORDER BY Cond, Split DESC;
