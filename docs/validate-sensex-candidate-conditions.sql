/*
================================================================================
Day-by-day stability check on the top SENSEX candidate buckets
================================================================================
READ-ONLY. Follow-up to docs/diagnose-sensex-stoploss-hits.sql Query 6.

A bucket with a great aggregate win rate but only 1-2 good days behind it is
noise, not edge (same bar SingleSignalRuleEvaluator's BANKNIFTY PUT BREAKDOWN
rule was held to: "6 of 8 day-cells above 50%"). This breaks the strongest
candidates down BY DAY to check that.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

WITH tagged AS (
    SELECT
        CAST(s.SnapshotTime AS DATE) AS TradeDate,
        o.FirstHitResult15Min,
        CASE
            WHEN o.Direction = 'PUT'  AND s.TrendDirection = 'Bearish' AND ISNULL(s.MarketState,'') = ''
                 AND s.Rsi >= 50 AND s.Rsi < 60 AND s.Pcr >= 0.80 AND s.Pcr < 1.00 AND s.AtrPct < 0.030
                THEN 'A: PUT blank-state RSI50-60 PCR0.8-1.0 ATR<0.03'
            WHEN o.Direction = 'CALL' AND s.TrendDirection = 'Bullish' AND ISNULL(s.MarketState,'') = ''
                 AND s.Rsi >= 70 AND s.Pcr >= 0.80 AND s.Pcr < 1.00 AND s.AtrPct < 0.030
                THEN 'B: CALL blank-state RSI70+ PCR0.8-1.0 ATR<0.03'
            WHEN o.Direction = 'CALL' AND s.TrendDirection = 'Bullish' AND ISNULL(s.MarketState,'') = ''
                 AND s.Rsi >= 60 AND s.Rsi < 70 AND s.Pcr >= 1.20 AND s.AtrPct >= 0.030 AND s.AtrPct < 0.050
                THEN 'C: CALL blank-state RSI60-70 PCR1.2+ ATR0.03-0.05'
            WHEN o.Direction = 'CALL' AND s.TrendDirection = 'Bullish' AND s.MarketState = 'StrongTrend'
                 AND s.Rsi >= 60 AND s.Rsi < 70 AND s.Pcr >= 1.00 AND s.Pcr < 1.20 AND s.AtrPct >= 0.030 AND s.AtrPct < 0.050
                THEN 'D: CALL StrongTrend RSI60-70 PCR1.0-1.2 ATR0.03-0.05'
            WHEN o.Direction = 'PUT' AND ISNULL(s.MarketState,'') = ''
                 AND s.Rsi >= 60 AND s.Rsi < 70 AND s.Pcr >= 1.00 AND s.Pcr < 1.20 AND s.AtrPct < 0.030
                THEN 'E: PUT blank-state RSI60-70 PCR1.0-1.2 ATR<0.03'
            ELSE NULL
        END AS Bucket
    FROM        dbo.IndicatorSnapshotOutcomes AS o
    INNER JOIN  dbo.IndicatorSnapshots        AS s ON s.Id = o.IndicatorSnapshotId
    WHERE       o.Symbol = 'SENSEX'
      AND       o.FirstHitResult15Min IS NOT NULL
      AND       o.Anomaly IS NULL
)
SELECT
    Bucket,
    TradeDate,
    COUNT(*)                                                          AS Samples,
    SUM(CASE WHEN FirstHitResult15Min = 'TARGET' THEN 1 ELSE 0 END)   AS Target15,
    SUM(CASE WHEN FirstHitResult15Min = 'SL'     THEN 1 ELSE 0 END)   AS SL15,
    CAST(100.0 * SUM(CASE WHEN FirstHitResult15Min = 'TARGET' THEN 1 ELSE 0 END)
         / NULLIF(SUM(CASE WHEN FirstHitResult15Min IN ('TARGET','SL') THEN 1 ELSE 0 END), 0)
         AS DECIMAL(6,2))                                             AS WinRate15
FROM   tagged
WHERE  Bucket IS NOT NULL
GROUP  BY Bucket, TradeDate
ORDER  BY Bucket, TradeDate;


-- Roll-up: how many days each bucket traded, and how many of those days were net-positive.
WITH tagged AS (
    SELECT
        CAST(s.SnapshotTime AS DATE) AS TradeDate,
        o.FirstHitResult15Min,
        CASE
            WHEN o.Direction = 'PUT'  AND s.TrendDirection = 'Bearish' AND ISNULL(s.MarketState,'') = ''
                 AND s.Rsi >= 50 AND s.Rsi < 60 AND s.Pcr >= 0.80 AND s.Pcr < 1.00 AND s.AtrPct < 0.030
                THEN 'A: PUT blank-state RSI50-60 PCR0.8-1.0 ATR<0.03'
            WHEN o.Direction = 'CALL' AND s.TrendDirection = 'Bullish' AND ISNULL(s.MarketState,'') = ''
                 AND s.Rsi >= 70 AND s.Pcr >= 0.80 AND s.Pcr < 1.00 AND s.AtrPct < 0.030
                THEN 'B: CALL blank-state RSI70+ PCR0.8-1.0 ATR<0.03'
            WHEN o.Direction = 'CALL' AND s.TrendDirection = 'Bullish' AND ISNULL(s.MarketState,'') = ''
                 AND s.Rsi >= 60 AND s.Rsi < 70 AND s.Pcr >= 1.20 AND s.AtrPct >= 0.030 AND s.AtrPct < 0.050
                THEN 'C: CALL blank-state RSI60-70 PCR1.2+ ATR0.03-0.05'
            WHEN o.Direction = 'CALL' AND s.TrendDirection = 'Bullish' AND s.MarketState = 'StrongTrend'
                 AND s.Rsi >= 60 AND s.Rsi < 70 AND s.Pcr >= 1.00 AND s.Pcr < 1.20 AND s.AtrPct >= 0.030 AND s.AtrPct < 0.050
                THEN 'D: CALL StrongTrend RSI60-70 PCR1.0-1.2 ATR0.03-0.05'
            WHEN o.Direction = 'PUT' AND ISNULL(s.MarketState,'') = ''
                 AND s.Rsi >= 60 AND s.Rsi < 70 AND s.Pcr >= 1.00 AND s.Pcr < 1.20 AND s.AtrPct < 0.030
                THEN 'E: PUT blank-state RSI60-70 PCR1.0-1.2 ATR<0.03'
            ELSE NULL
        END AS Bucket
    FROM        dbo.IndicatorSnapshotOutcomes AS o
    INNER JOIN  dbo.IndicatorSnapshots        AS s ON s.Id = o.IndicatorSnapshotId
    WHERE       o.Symbol = 'SENSEX'
      AND       o.FirstHitResult15Min IS NOT NULL
      AND       o.Anomaly IS NULL
),
byday AS (
    SELECT
        Bucket, TradeDate,
        SUM(CASE WHEN FirstHitResult15Min = 'TARGET' THEN 1 ELSE 0 END) AS T,
        SUM(CASE WHEN FirstHitResult15Min = 'SL'     THEN 1 ELSE 0 END) AS S
    FROM   tagged
    WHERE  Bucket IS NOT NULL
    GROUP  BY Bucket, TradeDate
)
SELECT
    Bucket,
    COUNT(*)                                              AS DaysTraded,
    SUM(CASE WHEN T > S THEN 1 ELSE 0 END)                AS DaysWinRateAbove50,
    SUM(CASE WHEN T + S >= 5 AND T <= S THEN 1 ELSE 0 END) AS DaysLosingWithVolume,
    SUM(T) AS TotalTarget, SUM(S) AS TotalSL
FROM   byday
GROUP  BY Bucket
ORDER  BY Bucket;
