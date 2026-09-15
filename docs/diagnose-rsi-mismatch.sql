/*
================================================================================
Diagnosing "chart RSI = 63 but IndicatorSnapshots.Rsi = 84.84"
================================================================================
READ-ONLY. SELECT only. READ UNCOMMITTED so it cannot block the live workers.

The engine computes RSI(14) on ONE-MINUTE candles only:
    MarketSnapshot.cs:126   ctx.Rsi.GetRsi(closes1m)
    MarketSnapshot.cs:18    decimal Rsi,   // 1m
There is no 5-minute or 15-minute RSI anywhere in the codebase.

A 1-minute RSI(14) is far more volatile than a 5-minute one and routinely
reaches 80+ on a move that reads ~63 on a 5-minute chart. So before assuming a
calculation bug, confirm which series your chart is on.

Set @Symbol and @At to the snapshot you are questioning, then run all four
queries. Candle rows are named '<SYMBOL>_1m' / '<SYMBOL>_5m'.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

DECLARE @Symbol  varchar(20)  = 'BANKNIFTY';
DECLARE @At      datetime2    = '2026-08-19 00:00:00';   -- <<< snapshot time to inspect
DECLARE @Sym1m   varchar(30)  = @Symbol + '_1m';
DECLARE @Sym5m   varchar(30)  = @Symbol + '_5m';


/* ----------------------------------------------------------------------------
   1. The snapshot row itself, for reference.
   ---------------------------------------------------------------------------- */
SELECT TOP (5)
       SnapshotTime, Symbol, Ltp, Ema20, Ema50, Ema20_5m, Ema50_5m,
       Rsi, AtrPct, PriceChangePct, TrendDirection, MarketState
FROM   dbo.IndicatorSnapshots
WHERE  Symbol = @Symbol
  AND  SnapshotTime <= @At
ORDER BY SnapshotTime DESC;


/* ----------------------------------------------------------------------------
   2. THE DECISIVE ONE — the last 20 one-minute closes feeding that RSI.
      Look at PtsChange. A run of consistent positive changes IS an RSI in the
      80s and the stored value is correct. Choppy +/- changes would mean the
      input series is wrong and the problem is real.
   ---------------------------------------------------------------------------- */
SELECT TOP (20)
       CandleTime,
       [Open], High, Low, [Close],
       [Close] - LAG([Close]) OVER (ORDER BY CandleTime) AS PtsChange
FROM   dbo.Candles
WHERE  Symbol = @Sym1m
  AND  CandleTime <= @At
ORDER BY CandleTime DESC;


/* ----------------------------------------------------------------------------
   3. Same window on FIVE-minute candles — this is what a 5m chart shows.
      If these closes look like your chart, your chart is 5m and the 1m RSI of
      84 is simply a different (correct) measurement.
   ---------------------------------------------------------------------------- */
SELECT TOP (20)
       CandleTime,
       [Open], High, Low, [Close],
       [Close] - LAG([Close]) OVER (ORDER BY CandleTime) AS PtsChange
FROM   dbo.Candles
WHERE  Symbol = @Sym5m
  AND  CandleTime <= @At
ORDER BY CandleTime DESC;


/* ----------------------------------------------------------------------------
   4. Data-quality check on the 1m series that feeds RSI.
      DuplicateMinutes must be 0 — there is no unique constraint on
      (Symbol, CandleTime), so duplicates are possible at the database level and
      would corrupt the close-to-close series.
      MissingMinutes counts gaps: CandleAggregator only emits a bar for minutes
      that actually received ticks, so a gap makes two non-adjacent closes look
      consecutive and inflates the measured move.
   ---------------------------------------------------------------------------- */
WITH recent AS (
    SELECT TOP (200) CandleTime, [Close]
    FROM   dbo.Candles
    WHERE  Symbol = @Sym1m AND CandleTime <= @At
    ORDER BY CandleTime DESC
),
gaps AS (
    SELECT CandleTime,
           DATEDIFF(MINUTE, LAG(CandleTime) OVER (ORDER BY CandleTime), CandleTime) AS MinuteStep
    FROM   recent
)
SELECT
    (SELECT COUNT(*)          FROM recent)                        AS RowsInspected,
    (SELECT COUNT(DISTINCT CandleTime) FROM recent)               AS DistinctMinutes,
    (SELECT COUNT(*) - COUNT(DISTINCT CandleTime) FROM recent)    AS DuplicateMinutes,
    (SELECT COUNT(*) FROM gaps WHERE MinuteStep > 1)              AS MissingMinutes,
    (SELECT MAX(MinuteStep) FROM gaps)                            AS LargestGapMinutes,
    (SELECT MIN(CandleTime) FROM recent)                          AS SeriesStart,
    (SELECT MAX(CandleTime) FROM recent)                          AS SeriesEnd;


/*
================================================================================
5. 1m vs 5m RSI comparison  (run once Rsi5m has been populating for a session)
================================================================================
Quantifies the gap between the RSI the engine trades on (1m) and the one your
Groww chart shows (5m). Rsi5m is NULL for roughly the first 75 minutes of each
session, until 15 closed 5m bars exist — those rows are excluded rather than
counted as a reading.

Use this before revisiting any RSI threshold: RsiCallFloor (55), RsiPutCeiling
(45), RsiBearConfirm (42), RsiBullConfirm (58), and the BANKNIFTY PUT BREAKDOWN
rule's Rsi <= 35 are all calibrated against the 1m column.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    Symbol,
    COUNT(*)                                                    AS Samples,
    CAST(AVG(Rsi)          AS DECIMAL(6,2))                     AS Avg1m,
    CAST(AVG(Rsi5m)        AS DECIMAL(6,2))                     AS Avg5m,
    CAST(AVG(Rsi - Rsi5m)  AS DECIMAL(6,2))                     AS AvgGap,
    CAST(MAX(ABS(Rsi - Rsi5m)) AS DECIMAL(6,2))                 AS MaxGap,

    -- How often each series is in "overbought"/"oversold" territory. A large
    -- difference here is the practical impact of the timeframe choice.
    SUM(CASE WHEN Rsi   >= 70 THEN 1 ELSE 0 END)                AS Over70_1m,
    SUM(CASE WHEN Rsi5m >= 70 THEN 1 ELSE 0 END)                AS Over70_5m,
    SUM(CASE WHEN Rsi   <= 35 THEN 1 ELSE 0 END)                AS Under35_1m,
    SUM(CASE WHEN Rsi5m <= 35 THEN 1 ELSE 0 END)                AS Under35_5m,

    -- Rows where the two disagree about which side of a live threshold we are on.
    SUM(CASE WHEN (Rsi <= 35) <> (Rsi5m <= 35) THEN 1 ELSE 0 END) AS DisagreeAt35,
    SUM(CASE WHEN (Rsi >= 55) <> (Rsi5m >= 55) THEN 1 ELSE 0 END) AS DisagreeAt55
FROM   dbo.IndicatorSnapshots
WHERE  Symbol IN ('NIFTY', 'BANKNIFTY', 'SENSEX')
  AND  Rsi5m IS NOT NULL
GROUP BY Symbol
ORDER BY Symbol;
