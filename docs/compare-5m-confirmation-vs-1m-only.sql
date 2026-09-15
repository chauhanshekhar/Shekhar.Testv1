/*
================================================================================
BACKTEST: OLD_1M_ONLY  vs  NEW_5M_CONFIRM_1M_ENTRY
================================================================================
READ-ONLY. SELECT only. READ UNCOMMITTED so it cannot block the live workers.

Measures the +20 / -20 INDEX-point first-hit outcome (IndicatorSnapshotOutcomes)
split by whether each snapshot would ALSO have passed the 5-minute confirmation.

    OLD_1M_ONLY              every eligible snapshot  (current production behaviour)
    NEW_5M_CONFIRM_1M_ENTRY  only those the 5m confirmation would have allowed

NO LOOK-AHEAD BIAS
Every field used for the confirmation — Ema20_5m, Ema50_5m, Rsi5m — was computed
in MarketSnapshotBuilder from CLOSED 5-minute candles as they stood AT THE
SNAPSHOT INSTANT (MarketSnapshotBuilder.IsClosedBar excludes the forming bar).
At 10:43 the row carries the 10:35-10:40 bar's values, never 10:40-10:45. The
outcome columns look forward, which is the point; the classifier columns do not.

The confirmation replicated below is exactly FiveMinuteConfirmationService:
    CALL: Ema20_5m > Ema50_5m AND Rsi5m >= RsiBullConfirm (58)
    PUT : Ema20_5m < Ema50_5m AND Rsi5m <= RsiBearConfirm (42)
Change the two constants here only if the live settings are changed.

PREREQUISITE
Rsi5m is only populated from the moment that column started being written, and
is NULL for ~the first 75 minutes of every session. Rows without it CANNOT be
classified and are excluded from BOTH arms so the comparison stays like-for-like.
Run the row-count check at the bottom first — if it returns few rows, let the
data accumulate before drawing conclusions.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

DECLARE @RsiBullConfirm decimal(18,4) = 58;
DECLARE @RsiBearConfirm decimal(18,4) = 42;

WITH classified AS (
    SELECT
        o.Symbol,
        o.Direction,
        o.FirstHitResult5Min,
        o.FirstHitResult10Min,
        o.FirstHitResult15Min,
        o.MaxFavorableMove15,
        o.MaxAdverseMove15,
        o.SecondsToFirstHit,

        CASE
            WHEN o.Direction = 'CALL'
                 AND s.Ema20_5m > s.Ema50_5m
                 AND s.Rsi5m   >= @RsiBullConfirm THEN 1
            WHEN o.Direction = 'PUT'
                 AND s.Ema20_5m < s.Ema50_5m
                 AND s.Rsi5m   <= @RsiBearConfirm THEN 1
            ELSE 0
        END AS PassesFiveMinuteConfirmation
    FROM        dbo.IndicatorSnapshotOutcomes AS o
    INNER JOIN  dbo.IndicatorSnapshots        AS s ON s.Id = o.IndicatorSnapshotId
    WHERE       o.Symbol IN ('NIFTY', 'BANKNIFTY', 'SENSEX')
      AND       o.FirstHitResult15Min IS NOT NULL     -- fully calculated only
      AND       o.Anomaly IS NULL
      AND       s.Rsi5m   IS NOT NULL                 -- classifiable only
      AND       s.Ema20_5m > 0
      AND       s.Ema50_5m > 0
),
arms AS (
    -- Arm A: current behaviour — every eligible snapshot.
    SELECT 'OLD_1M_ONLY' AS StrategyVersion, * FROM classified
    UNION ALL
    -- Arm B: only what the 5m confirmation would have permitted.
    SELECT 'NEW_5M_CONFIRM_1M_ENTRY', * FROM classified WHERE PassesFiveMinuteConfirmation = 1
)
SELECT
    Symbol,
    Direction,
    StrategyVersion,
    COUNT(*)                                                            AS Samples,

    SUM(CASE WHEN FirstHitResult5Min  = 'TARGET' THEN 1 ELSE 0 END)     AS TargetWins5,
    SUM(CASE WHEN FirstHitResult5Min  = 'SL'     THEN 1 ELSE 0 END)     AS SLHits5,
    CAST(100.0 * SUM(CASE WHEN FirstHitResult5Min = 'TARGET' THEN 1 ELSE 0 END)
         / NULLIF(SUM(CASE WHEN FirstHitResult5Min IN ('TARGET','SL') THEN 1 ELSE 0 END), 0)
         AS DECIMAL(6,2))                                               AS WinRate5,

    SUM(CASE WHEN FirstHitResult10Min = 'TARGET' THEN 1 ELSE 0 END)     AS TargetWins10,
    SUM(CASE WHEN FirstHitResult10Min = 'SL'     THEN 1 ELSE 0 END)     AS SLHits10,
    CAST(100.0 * SUM(CASE WHEN FirstHitResult10Min = 'TARGET' THEN 1 ELSE 0 END)
         / NULLIF(SUM(CASE WHEN FirstHitResult10Min IN ('TARGET','SL') THEN 1 ELSE 0 END), 0)
         AS DECIMAL(6,2))                                               AS WinRate10,

    SUM(CASE WHEN FirstHitResult15Min = 'TARGET' THEN 1 ELSE 0 END)     AS TargetWins15,
    SUM(CASE WHEN FirstHitResult15Min = 'SL'     THEN 1 ELSE 0 END)     AS SLHits15,
    CAST(100.0 * SUM(CASE WHEN FirstHitResult15Min = 'TARGET' THEN 1 ELSE 0 END)
         / NULLIF(SUM(CASE WHEN FirstHitResult15Min IN ('TARGET','SL') THEN 1 ELSE 0 END), 0)
         AS DECIMAL(6,2))                                               AS WinRate15,

    CAST(AVG(MaxFavorableMove15) AS DECIMAL(10,2))                      AS AvgMaxFavorable15,
    CAST(AVG(MaxAdverseMove15)   AS DECIMAL(10,2))                      AS AvgMaxAdverse15,

    -- Seconds from entry to the FIRST barrier touch, split by which barrier it was.
    CAST(AVG(CASE WHEN FirstHitResult15Min = 'TARGET'
                  THEN CAST(SecondsToFirstHit AS DECIMAL(10,2)) END) AS DECIMAL(10,1))
                                                                        AS AvgTimeToTargetSec,
    CAST(AVG(CASE WHEN FirstHitResult15Min = 'SL'
                  THEN CAST(SecondsToFirstHit AS DECIMAL(10,2)) END) AS DECIMAL(10,1))
                                                                        AS AvgTimeToSLSec
FROM     arms
GROUP BY Symbol, Direction, StrategyVersion
ORDER BY Symbol, Direction, StrategyVersion DESC;   -- OLD first, NEW under it


/*
================================================================================
HOW TO READ IT
================================================================================
For each Symbol+Direction the two rows are directly comparable.

The confirmation is only worth enabling if, for that Symbol+Direction:

  * WinRate15 rises MATERIALLY, and
  * Samples stays large enough to trust (thin arms are noise, not edge), and
  * AvgMaxAdverse15 improves or holds — a better win rate bought with much
    deeper drawdowns is not an improvement.

A large drop in Samples with only a small WinRate gain means the filter is
mostly discarding trades rather than discarding LOSING trades.

Enable per symbol, one at a time, via StrategySettings:
    SettingKey = 'UseFiveMinuteConfirmation', Symbol = '<SYMBOL>', SettingValue = 1
================================================================================
*/


/* ── Prerequisite check: is there enough classifiable data yet? ───────────── */

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    COUNT(*)                                                          AS OutcomeRows,
    SUM(CASE WHEN o.FirstHitResult15Min IS NOT NULL THEN 1 ELSE 0 END) AS FullyCalculated,
    SUM(CASE WHEN s.Rsi5m IS NOT NULL THEN 1 ELSE 0 END)              AS WithFiveMinuteRsi,
    SUM(CASE WHEN s.Rsi5m IS NOT NULL AND s.Ema20_5m > 0
                  AND o.FirstHitResult15Min IS NOT NULL
             THEN 1 ELSE 0 END)                                        AS UsableForComparison,
    MIN(s.SnapshotTime)                                               AS Oldest,
    MAX(s.SnapshotTime)                                               AS Newest
FROM        dbo.IndicatorSnapshotOutcomes AS o
INNER JOIN  dbo.IndicatorSnapshots        AS s ON s.Id = o.IndicatorSnapshotId
WHERE       o.Symbol IN ('NIFTY', 'BANKNIFTY', 'SENSEX');
