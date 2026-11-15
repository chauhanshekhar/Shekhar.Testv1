/* ============================================================================
   Is PCR >= GeminiGapUpPcrMin (1.1) actually reachable on gap-up days?   2026-10-21
   ----------------------------------------------------------------------------
   READ-ONLY. Run against the database you want to measure (e.g. TradingDB_Paper).
   Source: IndicatorSnapshots (Pcr, DayOpen, YesterdayClose, SnapshotTime in IST).

   Gap % = (DayOpen - YesterdayClose) / YesterdayClose * 100, the same definition as
   MarketRegimeService (gap up = > 0.30). Window = the Gemini entry window
   (09:30 - 11:15 IST: 15-minute opening range, 120 minutes after the open).

   CAVEAT: MarketSnapshotBuilder falls back to Pcr = 1.0 when no option chain row is
   available. "ExactlyOne" below counts those; a lot of them means the 1.1 test is
   being judged on missing data, not on real PCR.
   ============================================================================ */

SET NOCOUNT ON;

DECLARE @Days      int           = 90;     -- lookback
DECLARE @PcrMin    decimal(9,4)  = 1.1;    -- GeminiGapUpPcrMin
DECLARE @GapPct    decimal(9,4)  = 0.30;   -- gap threshold, percent
DECLARE @Start     time          = '09:30';
DECLARE @End       time          = '11:15';

;WITH s AS (
    SELECT i.Symbol, i.SnapshotTime, i.Pcr,
           CAST(i.SnapshotTime AS date) AS Day,
           g.GapPctValue
    FROM dbo.IndicatorSnapshots i WITH (NOLOCK)
    CROSS APPLY (SELECT CASE WHEN i.YesterdayClose > 0
                             THEN (i.DayOpen - i.YesterdayClose) / i.YesterdayClose * 100.0
                        END AS GapPctValue) g
    WHERE i.SnapshotTime >= DATEADD(DAY, -@Days, SYSDATETIME())
      AND CAST(i.SnapshotTime AS time) BETWEEN @Start AND @End
)
/* 1. PCR distribution per symbol, ALL days vs GAP-UP days (snapshots in the entry window) */
SELECT Symbol,
       CASE WHEN GapPctValue > @GapPct THEN 'GapUp' ELSE 'Other' END AS Opening,
       COUNT(*)                                             AS Snapshots,
       COUNT(DISTINCT Day)                                  AS Days,
       MIN(Pcr)                                             AS MinPcr,
       AVG(Pcr)                                             AS AvgPcr,
       MAX(Pcr)                                             AS MaxPcr,
       SUM(CASE WHEN Pcr >= @PcrMin THEN 1 ELSE 0 END)      AS AtOrAbovePcrMin,
       CAST(100.0 * SUM(CASE WHEN Pcr >= @PcrMin THEN 1 ELSE 0 END) / COUNT(*) AS decimal(6,2)) AS PctAtOrAbove,
       SUM(CASE WHEN Pcr = 1 THEN 1 ELSE 0 END)             AS ExactlyOne_MissingChainFallback
FROM s
WHERE GapPctValue IS NOT NULL
GROUP BY Symbol, CASE WHEN GapPctValue > @GapPct THEN 'GapUp' ELSE 'Other' END
ORDER BY Symbol, Opening;

/* 2. Each gap-up day: did PCR ever reach the threshold in the window? */
;WITH s AS (
    SELECT i.Symbol, CAST(i.SnapshotTime AS date) AS Day, i.Pcr,
           g.GapPctValue
    FROM dbo.IndicatorSnapshots i WITH (NOLOCK)
    CROSS APPLY (SELECT CASE WHEN i.YesterdayClose > 0
                             THEN (i.DayOpen - i.YesterdayClose) / i.YesterdayClose * 100.0
                        END AS GapPctValue) g
    WHERE i.SnapshotTime >= DATEADD(DAY, -@Days, SYSDATETIME())
      AND CAST(i.SnapshotTime AS time) BETWEEN @Start AND @End
)
SELECT Symbol, Day,
       CAST(MAX(GapPctValue) AS decimal(6,2)) AS GapPct,
       COUNT(*)                               AS Snapshots,
       MIN(Pcr) AS MinPcr, MAX(Pcr) AS MaxPcr,
       CASE WHEN MAX(Pcr) >= @PcrMin THEN 'REACHED' ELSE 'never' END AS ReachedPcrMin
FROM s
WHERE GapPctValue > @GapPct
GROUP BY Symbol, Day
ORDER BY Day DESC, Symbol;

/* 3. Overall PCR spread per symbol (percentiles) to see where 1.1 sits */
SELECT DISTINCT Symbol,
       PERCENTILE_CONT(0.10) WITHIN GROUP (ORDER BY Pcr) OVER (PARTITION BY Symbol) AS P10,
       PERCENTILE_CONT(0.50) WITHIN GROUP (ORDER BY Pcr) OVER (PARTITION BY Symbol) AS Median,
       PERCENTILE_CONT(0.90) WITHIN GROUP (ORDER BY Pcr) OVER (PARTITION BY Symbol) AS P90,
       PERCENTILE_CONT(0.99) WITHIN GROUP (ORDER BY Pcr) OVER (PARTITION BY Symbol) AS P99
FROM dbo.IndicatorSnapshots WITH (NOLOCK)
WHERE SnapshotTime >= DATEADD(DAY, -@Days, SYSDATETIME())
  AND Pcr > 0;
