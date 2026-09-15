/*
================================================================================
 SENSEX - does the DEPLOYED barrier geometry have positive expectancy?
================================================================================
READ-ONLY.

WHY THIS AND NOT ANOTHER +-20 SWEEP
The +-20 test measured a geometry the system does not trade. AtrRiskService.cs
computes, for every signal:
      stop = CLAMP(AtrRaw * AtrStopLossMultiplier(1.5), Min 20, Max 100)
      T1   = AtrRaw * AtrTarget1Multiplier(2.0)
      T2   = AtrRaw * AtrTarget2Multiplier(3.0)
On SENSEX at AtrPct 0.025 (AtrRaw ~20 pts) that is stop 30 / T1 40 / T2 61 index
points - not 20/20. A win rate measured at +-20 says nothing about a 30/40 book.

Also: 20 points on SENSEX (~81,000) is 0.025% of index. On BANKNIFTY (~57,000)
it is 0.035%, on NIFTY (~25,000) it is 0.08%. A FIXED point barrier is a
different test on every symbol, which is why the BANKNIFTY +-20 numbers could
never have transferred. This file uses each entry's own AtrRaw instead.

PART A - per-day baseline. Separates EDGE from DRIFT: if CALL loses and PUT wins
         on the same days, that is the market falling, not a rule working.
PART B - the deployed geometry, first-hit, with honest expectancy in points.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

DECLARE @Symbol      varchar(20)   = 'SENSEX';
DECLARE @Days        int           = 18;
DECLARE @HorizonMin  int           = 15;    -- also run at 30: a 2xATR target is
                                            -- ~40 pts and AvgMfe was only ~30
DECLARE @SlotMin     int           = 5;
DECLARE @LastEntry   time          = '15:10';

DECLARE @SlMult      decimal(10,4) = 1.5;   -- AtrStopLossMultiplier
DECLARE @T1Mult      decimal(10,4) = 2.0;   -- AtrTarget1Multiplier
DECLARE @T2Mult      decimal(10,4) = 3.0;   -- AtrTarget2Multiplier
DECLARE @MinStop     decimal(10,2) = 20;    -- MinimumIndexStopPoints
DECLARE @MaxStop     decimal(10,2) = 100;   -- MaximumIndexStopPoints


/* ============================================================================
   PART A - PER-DAY BASELINE AT +-20  (drift vs edge)
   ============================================================================
   Read the CALL and PUT columns side by side, day by day. A genuine directional
   edge wins on both sides across most days. A falling market shows PUT high and
   CALL low on the SAME days - that is the index moving, and it reverses when the
   market does.
   ============================================================================ */
WITH Candidate AS (
    SELECT s.Id, s.SnapshotTime, s.Symbol, s.Ltp,
           CAST(s.SnapshotTime AS date) AS TradeDate,
           DATEDIFF(MINUTE, '2000-01-01', s.SnapshotTime) / @SlotMin AS Slot,
           CASE WHEN s.TrendDirection = 'Bullish' AND s.Ema20_5m > s.Ema50_5m THEN 'CALL'
                WHEN s.TrendDirection = 'Bearish' AND s.Ema20_5m < s.Ema50_5m THEN 'PUT' END AS Direction
    FROM dbo.IndicatorSnapshots s
    WHERE s.Symbol = @Symbol
      AND s.SnapshotTime >= DATEADD(DAY, -@Days, CAST(GETDATE() AS date))
      AND CAST(s.SnapshotTime AS time) <= @LastEntry
      AND s.Ltp > 0 AND s.Ema20 > 0 AND s.Ema50 > 0
      AND s.Ema20_5m > 0 AND s.Ema50_5m > 0
      AND s.TrendDirection IN ('Bullish','Bearish')
),
Entries AS (
    SELECT * FROM (
        SELECT c.*, ROW_NUMBER() OVER (PARTITION BY c.Symbol, c.Direction, c.Slot ORDER BY c.SnapshotTime) rn
        FROM Candidate c WHERE c.Direction IS NOT NULL
    ) x WHERE rn = 1
),
Hit AS (
    SELECT e.TradeDate, e.Direction,
        MIN(CASE WHEN (e.Direction='CALL' AND f.Ltp >= e.Ltp + 20)
                   OR (e.Direction='PUT'  AND f.Ltp <= e.Ltp - 20) THEN f.SnapshotTime END) AS TTime,
        MIN(CASE WHEN (e.Direction='CALL' AND f.Ltp <= e.Ltp - 20)
                   OR (e.Direction='PUT'  AND f.Ltp >= e.Ltp + 20) THEN f.SnapshotTime END) AS STime,
        DATEDIFF(SECOND, e.SnapshotTime, MAX(f.SnapshotTime)) AS PathSeconds
    FROM Entries e
    JOIN dbo.IndicatorSnapshots f
      ON f.Symbol = e.Symbol AND f.Ltp > 0
     AND f.SnapshotTime > e.SnapshotTime
     AND f.SnapshotTime <= DATEADD(MINUTE, @HorizonMin, e.SnapshotTime)
    GROUP BY e.Id, e.TradeDate, e.Direction, e.SnapshotTime
)
SELECT
    TradeDate,
    SUM(CASE WHEN Direction='CALL' THEN 1 ELSE 0 END)                                  AS CallN,
    CAST(SUM(CASE WHEN Direction='CALL' AND TTime IS NOT NULL AND (STime IS NULL OR TTime<STime) THEN 1 ELSE 0 END) * 100.0
         / NULLIF(SUM(CASE WHEN Direction='CALL' AND (TTime IS NOT NULL OR STime IS NOT NULL) THEN 1 ELSE 0 END),0)
         AS DECIMAL(6,2))                                                              AS CallWin,
    SUM(CASE WHEN Direction='PUT' THEN 1 ELSE 0 END)                                   AS PutN,
    CAST(SUM(CASE WHEN Direction='PUT' AND TTime IS NOT NULL AND (STime IS NULL OR TTime<STime) THEN 1 ELSE 0 END) * 100.0
         / NULLIF(SUM(CASE WHEN Direction='PUT' AND (TTime IS NOT NULL OR STime IS NOT NULL) THEN 1 ELSE 0 END),0)
         AS DECIMAL(6,2))                                                              AS PutWin
FROM Hit
WHERE PathSeconds >= (@HorizonMin * 60) - 120
GROUP BY TradeDate
ORDER BY TradeDate;


/* ============================================================================
   PART B - THE DEPLOYED GEOMETRY, FIRST-HIT, WITH EXPECTANCY
   ============================================================================
   ExpPointsT1 is the number that decides everything: average index points per
   trade if every position exits at T1, SL, or the 15-minute mark.
       > 0  the geometry is at least not losing before costs
       <= 0 no threshold tuning inside this feature set will fix it

   It is still OPTIMISTIC: it ignores brokerage, option spread, the ~24s
   placement latency seen on signal 18, and theta on the NEITHER trades (which
   are scored at their index move, whereas a real option would have decayed).
   Treat a small positive as zero.
   ============================================================================ */
WITH Candidate AS (
    SELECT s.Id, s.SnapshotTime, s.Symbol, s.Ltp, s.AtrPct, s.AtrRaw, s.Rsi, s.Rsi5m,
           s.PriceChangePct, s.Ema20,
           CAST(s.SnapshotTime AS date) AS TradeDate,
           DATEDIFF(MINUTE, '2000-01-01', s.SnapshotTime) / @SlotMin AS Slot,
           CASE WHEN s.TrendDirection = 'Bullish' AND s.Ema20_5m > s.Ema50_5m THEN 'CALL'
                WHEN s.TrendDirection = 'Bearish' AND s.Ema20_5m < s.Ema50_5m THEN 'PUT' END AS Direction
    FROM dbo.IndicatorSnapshots s
    WHERE s.Symbol = @Symbol
      AND s.SnapshotTime >= DATEADD(DAY, -@Days, CAST(GETDATE() AS date))
      AND CAST(s.SnapshotTime AS time) <= @LastEntry
      AND s.Ltp > 0 AND s.Ema20 > 0 AND s.Ema50 > 0
      AND s.Ema20_5m > 0 AND s.Ema50_5m > 0 AND s.AtrRaw > 0
      AND s.TrendDirection IN ('Bullish','Bearish')
),
Entries AS (
    SELECT x.*,
           CASE WHEN x.AtrRaw * @SlMult < @MinStop THEN @MinStop
                WHEN x.AtrRaw * @SlMult > @MaxStop THEN @MaxStop
                ELSE x.AtrRaw * @SlMult END      AS SlDist,
           x.AtrRaw * @T1Mult                    AS T1Dist,
           x.AtrRaw * @T2Mult                    AS T2Dist
    FROM (
        SELECT c.*, ROW_NUMBER() OVER (PARTITION BY c.Symbol, c.Direction, c.Slot ORDER BY c.SnapshotTime) rn
        FROM Candidate c WHERE c.Direction IS NOT NULL
    ) x WHERE x.rn = 1
),
Hit AS (
    SELECT e.Id, e.Direction, e.TradeDate, e.AtrPct, e.SlDist, e.T1Dist, e.T2Dist,
        CASE WHEN ABS(e.PriceChangePct) >= 0.20 THEN 'MOVE_>=0.20' ELSE 'MOVE_<0.20' END AS MoveRange,
        CASE WHEN e.AtrPct < 0.025 THEN 'ATR_<0.025'
             WHEN e.AtrPct < 0.030 THEN 'ATR_0.025-0.029'
             ELSE 'ATR_0.030+' END AS AtrRange,
        MIN(CASE WHEN (e.Direction='CALL' AND f.Ltp >= e.Ltp + e.T1Dist)
                   OR (e.Direction='PUT'  AND f.Ltp <= e.Ltp - e.T1Dist) THEN f.SnapshotTime END) AS T1Time,
        MIN(CASE WHEN (e.Direction='CALL' AND f.Ltp >= e.Ltp + e.T2Dist)
                   OR (e.Direction='PUT'  AND f.Ltp <= e.Ltp - e.T2Dist) THEN f.SnapshotTime END) AS T2Time,
        MIN(CASE WHEN (e.Direction='CALL' AND f.Ltp <= e.Ltp - e.SlDist)
                   OR (e.Direction='PUT'  AND f.Ltp >= e.Ltp + e.SlDist) THEN f.SnapshotTime END) AS SlTime,
        -- Index move at the horizon, for trades that resolve neither way.
        MAX(CASE WHEN e.Direction='CALL' THEN f.Ltp - e.Ltp ELSE e.Ltp - f.Ltp END)              AS Mfe,
        MIN(CASE WHEN e.Direction='CALL' THEN f.Ltp - e.Ltp ELSE e.Ltp - f.Ltp END)              AS Mae,
        DATEDIFF(SECOND, e.SnapshotTime, MAX(f.SnapshotTime))                                    AS PathSeconds
    FROM Entries e
    JOIN dbo.IndicatorSnapshots f
      ON f.Symbol = e.Symbol AND f.Ltp > 0
     AND f.SnapshotTime > e.SnapshotTime
     AND f.SnapshotTime <= DATEADD(MINUTE, @HorizonMin, e.SnapshotTime)
    GROUP BY e.Id, e.Direction, e.TradeDate, e.Ltp, e.AtrPct, e.PriceChangePct,
             e.SlDist, e.T1Dist, e.T2Dist, e.SnapshotTime
),
Scored AS (
    SELECT h.*,
        CASE WHEN h.T1Time IS NOT NULL AND (h.SlTime IS NULL OR h.T1Time < h.SlTime) THEN 'T1'
             WHEN h.SlTime IS NOT NULL AND (h.T1Time IS NULL OR h.SlTime < h.T1Time) THEN 'SL'
             ELSE 'OPEN' END AS OutcomeT1,
        CASE WHEN h.T1Time IS NOT NULL AND (h.SlTime IS NULL OR h.T1Time < h.SlTime) THEN  h.T1Dist
             WHEN h.SlTime IS NOT NULL AND (h.T1Time IS NULL OR h.SlTime < h.T1Time) THEN -h.SlDist
             ELSE 0 END AS PointsT1
    FROM Hit h
    WHERE h.PathSeconds >= (@HorizonMin * 60) - 120
)
SELECT
    Direction, AtrRange, MoveRange,
    COUNT(*)                                                   AS Episodes,
    COUNT(DISTINCT TradeDate)                                  AS TradingDays,
    SUM(CASE WHEN OutcomeT1='T1'   THEN 1 ELSE 0 END)          AS T1First,
    SUM(CASE WHEN OutcomeT1='SL'   THEN 1 ELSE 0 END)          AS SlFirst,
    SUM(CASE WHEN OutcomeT1='OPEN' THEN 1 ELSE 0 END)          AS StillOpen,
    SUM(CASE WHEN T2Time IS NOT NULL AND (SlTime IS NULL OR T2Time < SlTime) THEN 1 ELSE 0 END) AS T2First,
    CAST(SUM(CASE WHEN OutcomeT1='T1' THEN 1 ELSE 0 END) * 100.0
         / NULLIF(SUM(CASE WHEN OutcomeT1 IN ('T1','SL') THEN 1 ELSE 0 END),0)
         AS DECIMAL(6,2))                                      AS WinRateResolved,
    CAST(AVG(SlDist) AS DECIMAL(10,2))                         AS AvgSlPts,
    CAST(AVG(T1Dist) AS DECIMAL(10,2))                         AS AvgT1Pts,
    -- THE NUMBER.
    CAST(AVG(PointsT1) AS DECIMAL(10,3))                       AS ExpPointsT1,
    CAST(AVG(Mfe) AS DECIMAL(10,2))                            AS AvgMfe,
    CAST(AVG(Mae) AS DECIMAL(10,2))                            AS AvgMae
FROM Scored
GROUP BY GROUPING SETS ((Direction), (Direction, AtrRange), (Direction, AtrRange, MoveRange))
ORDER BY Direction, AtrRange, MoveRange;
