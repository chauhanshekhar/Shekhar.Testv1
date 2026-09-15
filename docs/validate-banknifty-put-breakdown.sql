/*
================================================================================
BANKNIFTY PUT BREAKDOWN — read-only historical validation
================================================================================
READ-ONLY. SELECT statements only. Modifies no data, creates no objects.
READ UNCOMMITTED so it cannot take locks that block the live workers.

Rule under test (must match SingleSignalRuleEvaluator.EvaluateBankNiftyPutBreakdown
in src/Trading.SignalEngine/Strategies/SingleSignalRuleEvaluator.cs exactly):

    Symbol          = 'BANKNIFTY'
    TrendDirection  = 'Bearish'
    MarketState     = 'Breakdown'
    Ltp             <  Ema20
    Ema20           <  Ema50
    Ema20_5m        <  Ema50_5m
    Rsi             <= 35
    PriceChangePct  <= -0.20
    AtrPct          >= 0.030

Direction is PUT, so a FAVOURABLE move is the index FALLING:
    move = entry Ltp - future Ltp      (positive = good)

Expected, from the approved backtest:
    Samples 448 | 5m 73.66% | 10m 88.17% | 15m 94.64%
    Avg move +16.05 / +20.41 / +28.11 pts
    Worst 15m -22.25 | Best 15m +81.75
If the numbers below differ materially, DO NOT enable the rule — re-validate first.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

WITH matches AS (
    SELECT  s.Id,
            s.SnapshotTime,
            s.Ltp
    FROM    dbo.IndicatorSnapshots AS s
    WHERE   s.Symbol         =  'BANKNIFTY'
      AND   s.TrendDirection =  'Bearish'
      AND   s.MarketState    =  'Breakdown'
      AND   s.Ltp            <  s.Ema20
      AND   s.Ema20          <  s.Ema50
      AND   s.Ema20_5m       <  s.Ema50_5m
      AND   s.Rsi            <= 35
      AND   s.PriceChangePct <= -0.20
      AND   s.AtrPct         >= 0.030
),
forward AS (
    SELECT  m.Id,
            m.SnapshotTime,
            m.Ltp,
            -- Nearest snapshot at or after each horizon, capped at +1 minute of slack and
            -- constrained to the same calendar day so an overnight gap can never be counted
            -- as an intraday move.
            f5.Ltp  AS Ltp5,
            f10.Ltp AS Ltp10,
            f15.Ltp AS Ltp15
    FROM    matches AS m
    OUTER APPLY (
        SELECT TOP (1) x.Ltp FROM dbo.IndicatorSnapshots AS x
        WHERE  x.Symbol = 'BANKNIFTY'
          AND  x.SnapshotTime >= DATEADD(MINUTE,  5, m.SnapshotTime)
          AND  x.SnapshotTime <  DATEADD(MINUTE,  6, m.SnapshotTime)
          AND  CAST(x.SnapshotTime AS DATE) = CAST(m.SnapshotTime AS DATE)
        ORDER BY x.SnapshotTime
    ) AS f5
    OUTER APPLY (
        SELECT TOP (1) x.Ltp FROM dbo.IndicatorSnapshots AS x
        WHERE  x.Symbol = 'BANKNIFTY'
          AND  x.SnapshotTime >= DATEADD(MINUTE, 10, m.SnapshotTime)
          AND  x.SnapshotTime <  DATEADD(MINUTE, 11, m.SnapshotTime)
          AND  CAST(x.SnapshotTime AS DATE) = CAST(m.SnapshotTime AS DATE)
        ORDER BY x.SnapshotTime
    ) AS f10
    OUTER APPLY (
        SELECT TOP (1) x.Ltp FROM dbo.IndicatorSnapshots AS x
        WHERE  x.Symbol = 'BANKNIFTY'
          AND  x.SnapshotTime >= DATEADD(MINUTE, 15, m.SnapshotTime)
          AND  x.SnapshotTime <  DATEADD(MINUTE, 16, m.SnapshotTime)
          AND  CAST(x.SnapshotTime AS DATE) = CAST(m.SnapshotTime AS DATE)
        ORDER BY x.SnapshotTime
    ) AS f15
),
moves AS (
    SELECT  Id,
            SnapshotTime,
            Ltp - Ltp5  AS Move5,
            Ltp - Ltp10 AS Move10,
            Ltp - Ltp15 AS Move15
    FROM    forward
)
SELECT
    COUNT(*)                                                        AS Samples,
    COUNT(Move5)                                                    AS Samples5,
    COUNT(Move10)                                                   AS Samples10,
    COUNT(Move15)                                                   AS Samples15,

    CAST(100.0 * SUM(CASE WHEN Move5  > 0 THEN 1 ELSE 0 END)
         / NULLIF(COUNT(Move5),  0) AS DECIMAL(6,2))                AS WinRate5Pct,
    CAST(100.0 * SUM(CASE WHEN Move10 > 0 THEN 1 ELSE 0 END)
         / NULLIF(COUNT(Move10), 0) AS DECIMAL(6,2))                AS WinRate10Pct,
    CAST(100.0 * SUM(CASE WHEN Move15 > 0 THEN 1 ELSE 0 END)
         / NULLIF(COUNT(Move15), 0) AS DECIMAL(6,2))                AS WinRate15Pct,

    CAST(AVG(Move5)  AS DECIMAL(10,2))                              AS AvgMove5Pts,
    CAST(AVG(Move10) AS DECIMAL(10,2))                              AS AvgMove10Pts,
    CAST(AVG(Move15) AS DECIMAL(10,2))                              AS AvgMove15Pts,

    CAST(MIN(Move15) AS DECIMAL(10,2))                              AS Worst15Pts,
    CAST(MAX(Move15) AS DECIMAL(10,2))                              AS Best15Pts
FROM moves;


/*
--------------------------------------------------------------------------------
SUPPORTING CHECK — how many matches AtrGate would have blocked in live trading.
--------------------------------------------------------------------------------
The rule's ATR floor is 0.030, but AtrGate (a PRE-gate, running before the engine)
blocks anything below StrategySettings 'MinAtrPercent' for BANKNIFTY, which falls
back to 0.10 when no DB row exists. IndicatorSnapshots are written every cycle
regardless of gate outcome, so the backtest sample can include rows that live
trading would never have reached.

Run this to size that gap before enabling the rule.
--------------------------------------------------------------------------------
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT
    COUNT(*)                                                          AS RuleMatches,
    SUM(CASE WHEN AtrPct >= 0.10  THEN 1 ELSE 0 END)                  AS WouldPassAtrGate_At_0_10,
    SUM(CASE WHEN AtrPct <  0.10  THEN 1 ELSE 0 END)                  AS WouldBeBlocked_At_0_10,
    SUM(CASE WHEN AtrPct >= 0.05  THEN 1 ELSE 0 END)                  AS WouldPassAtrGate_At_0_05,
    CAST(MIN(AtrPct) AS DECIMAL(10,4))                                AS MinAtrPct,
    CAST(AVG(AtrPct) AS DECIMAL(10,4))                                AS AvgAtrPct,
    CAST(MAX(AtrPct) AS DECIMAL(10,4))                                AS MaxAtrPct
FROM    dbo.IndicatorSnapshots
WHERE   Symbol         =  'BANKNIFTY'
  AND   TrendDirection =  'Bearish'
  AND   MarketState    =  'Breakdown'
  AND   Ltp            <  Ema20
  AND   Ema20          <  Ema50
  AND   Ema20_5m       <  Ema50_5m
  AND   Rsi            <= 35
  AND   PriceChangePct <= -0.20
  AND   AtrPct         >= 0.030;


/*
--------------------------------------------------------------------------------
Current live AtrGate threshold for BANKNIFTY (blank result => the 0.10 fallback).
--------------------------------------------------------------------------------
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

SELECT SettingKey, Symbol, SettingValue, IsActive
FROM   dbo.StrategySettings
WHERE  SettingKey = 'MinAtrPercent'
  AND (Symbol = 'BANKNIFTY' OR Symbol IS NULL);
