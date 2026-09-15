/*
================================================================================
 Rule discovery on IndicatorSnapshots - CORRECTED MEASUREMENT  (v2)
================================================================================
READ-ONLY throughout. Run PART 1 first: it prints a row count after each filter,
so an empty result names its own cause instead of leaving you guessing.

WHAT CHANGED IN v2 (v1 returned nothing, and the fault was in v1)

 a. EPISODE DEFINITION WAS WRONG. v1 started a new episode only after a 5-minute
    GAP with no qualifying snapshot. But snapshots are written every cycle while
    a trend holds, so a two-hour bullish stretch collapsed to ONE episode. Across
    ~13 trading days that left too few episodes for any bucket to survive the
    HAVING clause. v2 uses fixed 5-minute SLOTS and takes the first qualifying
    snapshot in each - which is also what DuplicateSignalGate actually permits
    live: re-entry every 5 minutes while the setup persists.

 b. PathPoints >= 60 ASSUMED ~1 ROW/SECOND. SignalWorker delays 1 second between
    cycles (SignalWorker.cs:90) but processes every symbol sequentially inside
    the cycle, so real cadence is slower and unknown. A fixed point count can
    silently discard everything. v2 checks path COVERAGE by time instead, which
    does not care about cadence.

 c. Rsi5m WAS MANDATORY. Its migration is unapplied and the code that populates
    it is undeployed, so the column may be entirely NULL. Rsi5m is only used for
    bucketing here, so v2 keeps those rows under an 'R5_NA' bucket.

WHAT WAS CORRECT IN v1 AND IS UNCHANGED
    True first-hit (not +5/+10/+15 checkpoint sampling), conditional win rate,
    no truncated end-of-day paths, TradingDays reported for independence.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

DECLARE @Symbol       varchar(20)    = 'SENSEX';   -- SENSEX | NIFTY | BANKNIFTY
DECLARE @Days         int            = 18;
DECLARE @Barrier      decimal(10,2)  = 20;         -- index points, both sides
DECLARE @HorizonMin   int            = 15;
DECLARE @SlotMin      int            = 5;          -- DuplicateSignalGate cooldown
DECLARE @LastEntry    time           = '15:10';
DECLARE @MinEpisodes  int            = 20;         -- lower these two if PART 3
DECLARE @MinDays      int            = 3;          -- still comes back empty


/*
================================================================================
 PART 1 - WHERE DOES THE DATA DISAPPEAR
================================================================================
Run this alone. The first Rows value that is 0 (or far smaller than the line
above it) is the filter to relax. Line 6 dropping to 0 means Rsi5m is unpopulated
- expected until the AddRsi5mToIndicatorSnapshot migration is applied AND the
worker rebuilt.
================================================================================
*/
SELECT '1. rows for symbol'         AS Stage, COUNT(*) AS Rows FROM dbo.IndicatorSnapshots WHERE Symbol = @Symbol
UNION ALL
SELECT '2. + inside date window',   COUNT(*) FROM dbo.IndicatorSnapshots WHERE Symbol = @Symbol
    AND SnapshotTime >= DATEADD(DAY, -@Days, CAST(GETDATE() AS date))
UNION ALL
SELECT '3. + before 15:10',         COUNT(*) FROM dbo.IndicatorSnapshots WHERE Symbol = @Symbol
    AND SnapshotTime >= DATEADD(DAY, -@Days, CAST(GETDATE() AS date))
    AND CAST(SnapshotTime AS time) <= @LastEntry
UNION ALL
SELECT '4. + Bullish/Bearish',      COUNT(*) FROM dbo.IndicatorSnapshots WHERE Symbol = @Symbol
    AND SnapshotTime >= DATEADD(DAY, -@Days, CAST(GETDATE() AS date))
    AND CAST(SnapshotTime AS time) <= @LastEntry
    AND TrendDirection IN ('Bullish','Bearish')
UNION ALL
SELECT '5. + 1m EMAs present',      COUNT(*) FROM dbo.IndicatorSnapshots WHERE Symbol = @Symbol
    AND SnapshotTime >= DATEADD(DAY, -@Days, CAST(GETDATE() AS date))
    AND CAST(SnapshotTime AS time) <= @LastEntry
    AND TrendDirection IN ('Bullish','Bearish')
    AND Ltp > 0 AND Ema20 > 0 AND Ema50 > 0
UNION ALL
SELECT '6. + 5m EMAs present',      COUNT(*) FROM dbo.IndicatorSnapshots WHERE Symbol = @Symbol
    AND SnapshotTime >= DATEADD(DAY, -@Days, CAST(GETDATE() AS date))
    AND CAST(SnapshotTime AS time) <= @LastEntry
    AND TrendDirection IN ('Bullish','Bearish')
    AND Ltp > 0 AND Ema20 > 0 AND Ema50 > 0
    AND Ema20_5m > 0 AND Ema50_5m > 0
UNION ALL
SELECT '7. + 5m EMAs AGREE w/ trend', COUNT(*) FROM dbo.IndicatorSnapshots WHERE Symbol = @Symbol
    AND SnapshotTime >= DATEADD(DAY, -@Days, CAST(GETDATE() AS date))
    AND CAST(SnapshotTime AS time) <= @LastEntry
    AND Ltp > 0 AND Ema20 > 0 AND Ema50 > 0
    AND Ema20_5m > 0 AND Ema50_5m > 0
    AND ((TrendDirection = 'Bullish' AND Ema20_5m > Ema50_5m)
      OR (TrendDirection = 'Bearish' AND Ema20_5m < Ema50_5m))
UNION ALL
SELECT '8. of those, Rsi5m populated', COUNT(*) FROM dbo.IndicatorSnapshots WHERE Symbol = @Symbol
    AND SnapshotTime >= DATEADD(DAY, -@Days, CAST(GETDATE() AS date))
    AND CAST(SnapshotTime AS time) <= @LastEntry
    AND Ltp > 0 AND Ema20 > 0 AND Ema50 > 0
    AND Ema20_5m > 0 AND Ema50_5m > 0
    AND ((TrendDirection = 'Bullish' AND Ema20_5m > Ema50_5m)
      OR (TrendDirection = 'Bearish' AND Ema20_5m < Ema50_5m))
    AND Rsi5m IS NOT NULL;


/*
================================================================================
 PART 2 - ACTUAL SNAPSHOT CADENCE
================================================================================
Everything downstream depends on this. It sets how finely the path is resolved
and therefore how trustworthy "first hit" is. A median gap of 1-3 seconds is
good resolution; 30+ seconds means barrier touches are being missed entirely and
NO first-hit backtest on this table can be trusted.
================================================================================
*/
WITH gaps AS (
    SELECT DATEDIFF(SECOND,
               LAG(SnapshotTime) OVER (PARTITION BY Symbol, CAST(SnapshotTime AS date) ORDER BY SnapshotTime),
               SnapshotTime) AS GapSec
    FROM dbo.IndicatorSnapshots
    WHERE Symbol = @Symbol
      AND SnapshotTime >= DATEADD(DAY, -3, CAST(GETDATE() AS date))
)
SELECT
    COUNT(*)                                             AS Gaps,
    MIN(GapSec)                                          AS MinGapSec,
    CAST(AVG(CAST(GapSec AS float)) AS DECIMAL(10,2))    AS AvgGapSec,
    MAX(GapSec)                                          AS MaxGapSec,
    SUM(CASE WHEN GapSec > 60 THEN 1 ELSE 0 END)         AS GapsOverAMinute
FROM gaps
WHERE GapSec IS NOT NULL AND GapSec BETWEEN 0 AND 3600;


/*
================================================================================
 PART 3 - THE SWEEP
================================================================================
*/
WITH Candidate AS (
    SELECT
        s.Id, s.SnapshotTime, s.Symbol, s.Ltp,
        s.Rsi, s.Rsi5m, s.AtrPct, s.PriceChangePct, s.Ema20,
        CAST(s.SnapshotTime AS date) AS TradeDate,
        -- Fixed 5-minute slot: the unit DuplicateSignalGate actually permits.
        DATEDIFF(MINUTE, '2000-01-01', s.SnapshotTime) / @SlotMin AS Slot,
        CASE
            WHEN s.TrendDirection = 'Bullish' AND s.Ema20_5m > s.Ema50_5m THEN 'CALL'
            WHEN s.TrendDirection = 'Bearish' AND s.Ema20_5m < s.Ema50_5m THEN 'PUT'
        END AS Direction
    FROM dbo.IndicatorSnapshots s
    WHERE s.Symbol = @Symbol
      AND s.SnapshotTime >= DATEADD(DAY, -@Days, CAST(GETDATE() AS date))
      AND CAST(s.SnapshotTime AS time) <= @LastEntry
      AND s.Ltp > 0 AND s.Ema20 > 0 AND s.Ema50 > 0
      AND s.Ema20_5m > 0 AND s.Ema50_5m > 0
      AND s.TrendDirection IN ('Bullish','Bearish')
      -- Rsi5m deliberately NOT required: it only drives a bucket label below.
),

/* One entry per 5-minute slot per direction. */
Entries AS (
    SELECT * FROM (
        SELECT c.*,
               ROW_NUMBER() OVER (PARTITION BY c.Symbol, c.Direction, c.Slot
                                  ORDER BY c.SnapshotTime) AS rn
        FROM Candidate c
        WHERE c.Direction IS NOT NULL
    ) x WHERE rn = 1
),

/* Both barriers and the full excursion in ONE pass over the path. */
Path AS (
    SELECT
        e.Id,
        MIN(CASE WHEN (e.Direction = 'CALL' AND f.Ltp >= e.Ltp + @Barrier)
                   OR (e.Direction = 'PUT'  AND f.Ltp <= e.Ltp - @Barrier)
                 THEN f.SnapshotTime END)                              AS TargetTime,
        MIN(CASE WHEN (e.Direction = 'CALL' AND f.Ltp <= e.Ltp - @Barrier)
                   OR (e.Direction = 'PUT'  AND f.Ltp >= e.Ltp + @Barrier)
                 THEN f.SnapshotTime END)                              AS SlTime,
        MAX(CASE WHEN e.Direction = 'CALL' THEN f.Ltp - e.Ltp
                                           ELSE e.Ltp - f.Ltp END)     AS Mfe,
        MIN(CASE WHEN e.Direction = 'CALL' THEN f.Ltp - e.Ltp
                                           ELSE e.Ltp - f.Ltp END)     AS Mae,
        COUNT(*)                                                       AS PathPoints,
        -- Coverage in TIME, so the check is independent of snapshot cadence.
        DATEDIFF(SECOND, e.SnapshotTime, MAX(f.SnapshotTime))          AS PathSeconds
    FROM Entries e
    JOIN dbo.IndicatorSnapshots f
      ON  f.Symbol       = e.Symbol
      AND f.SnapshotTime > e.SnapshotTime
      AND f.SnapshotTime <= DATEADD(MINUTE, @HorizonMin, e.SnapshotTime)
      AND f.Ltp > 0
    GROUP BY e.Id, e.SnapshotTime
),

Scored AS (
    SELECT
        e.*,
        p.Mfe, p.Mae, p.PathPoints,
        CASE
            WHEN p.TargetTime IS NOT NULL
             AND (p.SlTime IS NULL OR p.TargetTime < p.SlTime) THEN 'TARGET'
            WHEN p.SlTime IS NOT NULL
             AND (p.TargetTime IS NULL OR p.SlTime < p.TargetTime) THEN 'SL'
            WHEN p.TargetTime IS NOT NULL AND p.TargetTime = p.SlTime THEN 'AMBIGUOUS'
            ELSE 'NEITHER'
        END AS Outcome,

        CASE WHEN e.Rsi5m IS NULL      THEN 'R5_NA'
             WHEN e.Rsi5m < 40 THEN 'R5_<40'   WHEN e.Rsi5m < 45 THEN 'R5_40-44'
             WHEN e.Rsi5m < 50 THEN 'R5_45-49' WHEN e.Rsi5m < 55 THEN 'R5_50-54'
             WHEN e.Rsi5m < 60 THEN 'R5_55-59' WHEN e.Rsi5m < 65 THEN 'R5_60-64'
             WHEN e.Rsi5m < 70 THEN 'R5_65-69' ELSE 'R5_70+' END AS Rsi5mRange,

        CASE WHEN e.AtrPct < 0.025 THEN 'ATR_<0.025'
             WHEN e.AtrPct < 0.030 THEN 'ATR_0.025-0.029'
             WHEN e.AtrPct < 0.040 THEN 'ATR_0.030-0.039'
             ELSE 'ATR_0.040+' END AS AtrRange,

        -- The live rule's momentary move condition, absent from the first sweep.
        CASE WHEN ABS(e.PriceChangePct) >= 0.20 THEN 'MOVE_>=0.20'
             ELSE 'MOVE_<0.20' END AS MoveRange,

        -- Ltp vs Ema20: extended past the fast EMA, also absent originally.
        CASE WHEN (e.Direction = 'CALL' AND e.Ltp > e.Ema20)
                OR (e.Direction = 'PUT'  AND e.Ltp < e.Ema20)
             THEN 'EXTENDED' ELSE 'PULLBACK' END AS ExtensionState
    FROM Entries e
    JOIN Path p ON p.Id = e.Id
    -- The window must be substantially observed, or NEITHER is a data artefact.
    WHERE p.PathSeconds >= (@HorizonMin * 60) - 120
)

SELECT
    Direction, Rsi5mRange, AtrRange, MoveRange, ExtensionState,

    COUNT(*)                                              AS Episodes,
    COUNT(DISTINCT TradeDate)                             AS TradingDays,

    SUM(CASE WHEN Outcome = 'TARGET'  THEN 1 ELSE 0 END)  AS TargetFirst,
    SUM(CASE WHEN Outcome = 'SL'      THEN 1 ELSE 0 END)  AS SlFirst,
    SUM(CASE WHEN Outcome = 'NEITHER' THEN 1 ELSE 0 END)  AS Neither,

    -- THE decision number: of the trades that resolved, how many won.
    CAST(SUM(CASE WHEN Outcome = 'TARGET' THEN 1 ELSE 0 END) * 100.0
         / NULLIF(SUM(CASE WHEN Outcome IN ('TARGET','SL') THEN 1 ELSE 0 END), 0)
         AS DECIMAL(6,2))                                 AS WinRateResolved,

    CAST(AVG(Mfe) AS DECIMAL(10,2))                       AS AvgMfe,
    CAST(AVG(Mae) AS DECIMAL(10,2))                       AS AvgMae,
    CAST(MIN(Mae) AS DECIMAL(10,2))                       AS WorstMae
FROM Scored
GROUP BY Direction, Rsi5mRange, AtrRange, MoveRange, ExtensionState
HAVING COUNT(*) >= @MinEpisodes
   AND COUNT(DISTINCT TradeDate) >= @MinDays
ORDER BY WinRateResolved DESC, Episodes DESC;


/*
================================================================================
 PART 4 - UNGROUPED BASELINE
================================================================================
Always returns a row if PART 3's pipeline produced ANY episode. If this is empty
while PART 1 line 7 is not, the loss is in Path/PathSeconds - re-read PART 2.
Its win rate is also the number every bucket in PART 3 must BEAT to be worth
anything; a bucket at 60% when the baseline is 58% has found nothing.
================================================================================
*/
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
        SELECT c.*, ROW_NUMBER() OVER (PARTITION BY c.Symbol, c.Direction, c.Slot ORDER BY c.SnapshotTime) AS rn
        FROM Candidate c WHERE c.Direction IS NOT NULL
    ) x WHERE rn = 1
),
Path AS (
    SELECT e.Id, e.Direction, e.TradeDate,
        MIN(CASE WHEN (e.Direction='CALL' AND f.Ltp >= e.Ltp + @Barrier)
                   OR (e.Direction='PUT'  AND f.Ltp <= e.Ltp - @Barrier) THEN f.SnapshotTime END) AS TargetTime,
        MIN(CASE WHEN (e.Direction='CALL' AND f.Ltp <= e.Ltp - @Barrier)
                   OR (e.Direction='PUT'  AND f.Ltp >= e.Ltp + @Barrier) THEN f.SnapshotTime END) AS SlTime,
        DATEDIFF(SECOND, e.SnapshotTime, MAX(f.SnapshotTime)) AS PathSeconds
    FROM Entries e
    JOIN dbo.IndicatorSnapshots f
      ON f.Symbol = e.Symbol
     AND f.SnapshotTime > e.SnapshotTime
     AND f.SnapshotTime <= DATEADD(MINUTE, @HorizonMin, e.SnapshotTime)
     AND f.Ltp > 0
    GROUP BY e.Id, e.Direction, e.TradeDate, e.SnapshotTime
)
SELECT
    Direction,
    COUNT(*)                    AS Episodes,
    COUNT(DISTINCT TradeDate)   AS TradingDays,
    SUM(CASE WHEN TargetTime IS NOT NULL AND (SlTime IS NULL OR TargetTime < SlTime) THEN 1 ELSE 0 END) AS TargetFirst,
    SUM(CASE WHEN SlTime IS NOT NULL AND (TargetTime IS NULL OR SlTime < TargetTime) THEN 1 ELSE 0 END) AS SlFirst,
    SUM(CASE WHEN TargetTime IS NULL AND SlTime IS NULL THEN 1 ELSE 0 END)                              AS Neither,
    CAST(SUM(CASE WHEN TargetTime IS NOT NULL AND (SlTime IS NULL OR TargetTime < SlTime) THEN 1 ELSE 0 END) * 100.0
         / NULLIF(SUM(CASE WHEN TargetTime IS NOT NULL OR SlTime IS NOT NULL THEN 1 ELSE 0 END), 0)
         AS DECIMAL(6,2)) AS WinRateResolved
FROM Path
WHERE PathSeconds >= (@HorizonMin * 60) - 120
GROUP BY Direction
ORDER BY Direction;
