/*
  research-gapup-combos.sql  (READ-ONLY)
  Corrected version of the gap-up quintile-combo query.

  Fixes vs. the original:
   1. One row per setup (first snapshot per Symbol/Direction/day/15-min bucket) - not per snapshot.
      Dedupe happens BEFORE the outcome filter, so selection does not depend on the outcome.
   2. Join guard: outcome.EntryPrice must equal snapshot.Ltp (IndicatorSnapshotOutcomes FK has been unreliable).
   3. Data window starts after the 5m-candle fix (2026-09-30 13:30 IST).
   4. Quintile edges are computed on TRAIN days only (first half of days) and applied to VALIDATION days.
   5. Unfiltered BASE row per split, so every combo is compared with its base rate.
   6. Distinct-day counts computed correctly; PositiveSessionPct fixed.
   7. No fake P&L: report win rate and NetR (Wins - Losses, in R units, UNCOSTED). Not points.
  NULL outcome = not yet calculated -> excluded. NONE (no hit in 15 min) -> excluded from W/L, counted separately.
  A combo is only worth a shadow test if PassBoth = 1. Multiple-comparison noise is still possible: expect some false passes.
*/
DECLARE @From          datetime2    = '2026-09-30 13:30';
DECLARE @MinGapPct     float        = 0.30;
DECLARE @BucketMin     int          = 15;
DECLARE @EntryTol      decimal(18,4)= 0.05;   -- |Outcome.EntryPrice - Snapshot.Ltp| tolerance
DECLARE @MinTrades     int          = 15;     -- per split
DECLARE @MinDays       int          = 3;      -- per split

;WITH Cand AS
(
    SELECT
        CAST(S.SnapshotTime AS date) AS TradeDate,
        S.SnapshotTime, S.Id AS SnapshotId, S.Symbol, D.Direction,
        O.FirstHitResult15Min AS Outcome,
        (S.DayOpen - S.YesterdayClose) * 100.0 / NULLIF(S.YesterdayClose, 0) AS GapPct,
        S.PriceChangePct, S.AtrRaw, S.Rsi5m, S.Pcr, S.Ltp, S.Vwap, S.TrendDirection,
        CASE WHEN S.Ema20    > S.Ema50    THEN 'Bullish' WHEN S.Ema20    < S.Ema50    THEN 'Bearish' ELSE 'Neutral' END AS EMA1m,
        CASE WHEN S.Ema20_5m > S.Ema50_5m THEN 'Bullish' WHEN S.Ema20_5m < S.Ema50_5m THEN 'Bearish' ELSE 'Neutral' END AS EMA5m
    FROM dbo.IndicatorSnapshots S
    CROSS JOIN (VALUES ('CALL'), ('PUT')) D(Direction)
    CROSS APPLY
    (
        SELECT TOP (1) X.FirstHitResult15Min, X.EntryPrice
        FROM dbo.IndicatorSnapshotOutcomes X
        WHERE X.IndicatorSnapshotId = S.Id
          AND X.Direction = D.Direction
        ORDER BY X.CalculatedOn DESC, X.Id DESC
    ) O
    WHERE S.SnapshotTime >= @From
      AND S.YesterdayClose > 0
      AND S.DayOpen > S.YesterdayClose
      AND (S.DayOpen - S.YesterdayClose) * 100.0 / S.YesterdayClose >= @MinGapPct
      AND ABS(O.EntryPrice - S.Ltp) <= @EntryTol          -- join-integrity guard
),
Deduped AS
(
    SELECT *,
        ROW_NUMBER() OVER
        (
            PARTITION BY Symbol, Direction, TradeDate,
                         DATEDIFF(MINUTE, CAST(SnapshotTime AS date), SnapshotTime) / @BucketMin
            ORDER BY SnapshotTime, SnapshotId
        ) AS rn
    FROM Cand
),
Setups AS
(
    SELECT *
    FROM Deduped
    WHERE rn = 1
      AND Outcome IN ('TARGET', 'SL')                     -- NULL (uncalculated) and NONE excluded
),
DayRank AS
(
    SELECT TradeDate,
           DENSE_RANK() OVER (ORDER BY TradeDate) AS DayNo,
           (SELECT COUNT(DISTINCT TradeDate) FROM Setups) AS TotalDays
    FROM Setups
    GROUP BY TradeDate
),
Split AS
(
    SELECT S.*,
           CASE WHEN R.DayNo <= (R.TotalDays + 1) / 2 THEN 'TRAIN' ELSE 'VALID' END AS SplitName
    FROM Setups S
    JOIN DayRank R ON R.TradeDate = S.TradeDate
),
Long AS
(
    SELECT B.SnapshotId, B.Symbol, B.Direction, B.SplitName, V.FeatureName, V.FeatureValue
    FROM Split B
    CROSS APPLY (VALUES
        ('GapPct',          CONVERT(decimal(18,6), B.GapPct)),
        ('PriceChangePct',  CONVERT(decimal(18,6), B.PriceChangePct)),
        ('ATR',             CONVERT(decimal(18,6), B.AtrRaw)),
        ('RSI5m',           CONVERT(decimal(18,6), B.Rsi5m)),
        ('PCR',             CONVERT(decimal(18,6), B.Pcr)),
        ('VWAPDistancePct', CONVERT(decimal(18,6), (B.Ltp - B.Vwap) * 100.0 / NULLIF(B.Vwap, 0)))
    ) V(FeatureName, FeatureValue)
    WHERE V.FeatureValue IS NOT NULL
),
TrainBinned AS      -- edges from TRAIN only
(
    SELECT Symbol, Direction, FeatureName, FeatureValue,
           NTILE(5) OVER (PARTITION BY Direction, Symbol, FeatureName ORDER BY FeatureValue, SnapshotId) AS Band
    FROM Long
    WHERE SplitName = 'TRAIN'
),
Edges AS
(
    SELECT Symbol, Direction, FeatureName, Band, MIN(FeatureValue) AS Lo
    FROM TrainBinned
    GROUP BY Symbol, Direction, FeatureName, Band
),
Banded AS           -- apply train edges to every row (train and valid)
(
    SELECT L.SnapshotId, L.Symbol, L.Direction, L.FeatureName,
           ISNULL(E.Band, 1) AS Band
    FROM Long L
    OUTER APPLY
    (
        SELECT TOP (1) Band
        FROM Edges X
        WHERE X.Symbol = L.Symbol AND X.Direction = L.Direction AND X.FeatureName = L.FeatureName
          AND X.Lo <= L.FeatureValue
        ORDER BY X.Band DESC
    ) E
),
Bins AS
(
    SELECT B.SnapshotId, B.Direction,
        MAX(CASE WHEN FeatureName = 'GapPct'         THEN Band END) AS GapQ,
        MAX(CASE WHEN FeatureName = 'PriceChangePct' THEN Band END) AS PriceChangeQ,
        MAX(CASE WHEN FeatureName = 'ATR'            THEN Band END) AS ATRQ,
        MAX(CASE WHEN FeatureName = 'RSI5m'          THEN Band END) AS RSI5mQ,
        MAX(CASE WHEN FeatureName = 'PCR'            THEN Band END) AS PCRQ,
        MAX(CASE WHEN FeatureName = 'VWAPDistancePct'THEN Band END) AS VWAPQ
    FROM Banded B
    GROUP BY B.SnapshotId, B.Direction          -- Direction in key: same snapshot can appear for CALL and PUT
),
ComboRows AS
(
    -- every setup gets a BASE row; combos below add their own
    SELECT SplitName, TradeDate, Symbol, Direction, Outcome, 'BASE (unfiltered gap-up)' AS ComboName, 'all' AS Condition
    FROM Split

    UNION ALL
    SELECT S.SplitName, S.TradeDate, S.Symbol, S.Direction, S.Outcome,
           'CALL: PriceChange + EMA5m + ATR',
           CONCAT('PriceChange Q', N.PriceChangeQ, ' + EMA5m ', S.EMA5m, ' + ATR Q', N.ATRQ)
    FROM Split S JOIN Bins N ON N.SnapshotId = S.SnapshotId AND N.Direction = S.Direction
    WHERE S.Direction = 'CALL' AND N.PriceChangeQ IS NOT NULL AND N.ATRQ IS NOT NULL

    UNION ALL
    SELECT S.SplitName, S.TradeDate, S.Symbol, S.Direction, S.Outcome,
           'PUT: ATR + RSI5m + EMA5m',
           CONCAT('ATR Q', N.ATRQ, ' + RSI5m Q', N.RSI5mQ, ' + EMA5m ', S.EMA5m)
    FROM Split S JOIN Bins N ON N.SnapshotId = S.SnapshotId AND N.Direction = S.Direction
    WHERE S.Direction = 'PUT' AND N.ATRQ IS NOT NULL AND N.RSI5mQ IS NOT NULL

    UNION ALL
    SELECT S.SplitName, S.TradeDate, S.Symbol, S.Direction, S.Outcome,
           'PUT: PCR + ATR + VWAP',
           CONCAT('PCR Q', N.PCRQ, ' + ATR Q', N.ATRQ, ' + VWAP Q', N.VWAPQ)
    FROM Split S JOIN Bins N ON N.SnapshotId = S.SnapshotId AND N.Direction = S.Direction
    WHERE S.Direction = 'PUT' AND N.PCRQ IS NOT NULL AND N.ATRQ IS NOT NULL AND N.VWAPQ IS NOT NULL

    UNION ALL
    SELECT S.SplitName, S.TradeDate, S.Symbol, S.Direction, S.Outcome,
           'CALL/PUT: Gap + Trend + EMA',
           CONCAT('Gap Q', N.GapQ, ' + Trend ', COALESCE(S.TrendDirection, 'Unknown'),
                  ' + EMA1m ', S.EMA1m, ' + EMA5m ', S.EMA5m)
    FROM Split S JOIN Bins N ON N.SnapshotId = S.SnapshotId AND N.Direction = S.Direction
    WHERE N.GapQ IS NOT NULL
),
DailyStats AS
(
    SELECT SplitName, TradeDate, Direction, Symbol, ComboName, Condition,
           SUM(CASE WHEN Outcome = 'TARGET' THEN 1 ELSE 0 END) AS Wins,
           SUM(CASE WHEN Outcome = 'SL'     THEN 1 ELSE 0 END) AS Losses
    FROM ComboRows
    GROUP BY SplitName, TradeDate, Direction, Symbol, ComboName, Condition
),
SplitStats AS
(
    SELECT SplitName, Direction, Symbol, ComboName, Condition,
           SUM(Wins + Losses)                         AS Trades,
           SUM(Wins)                                  AS Wins,
           SUM(Losses)                                AS Losses,
           COUNT(*)                                   AS Days,
           SUM(CASE WHEN Wins > Losses THEN 1 ELSE 0 END) AS PositiveDays,
           CAST(100.0 * SUM(Wins) / NULLIF(SUM(Wins + Losses), 0) AS decimal(10,2)) AS WinRate,
           SUM(Wins) - SUM(Losses)                    AS NetR_Uncosted
    FROM DailyStats
    GROUP BY SplitName, Direction, Symbol, ComboName, Condition
),
WithBase AS
(
    SELECT S.*, B.WinRate AS BaseWinRate
    FROM SplitStats S
    JOIN SplitStats B
      ON B.SplitName = S.SplitName AND B.Direction = S.Direction AND B.Symbol = S.Symbol
     AND B.ComboName = 'BASE (unfiltered gap-up)'
)
SELECT
    T.Direction, T.Symbol, T.ComboName, T.Condition,

    T.Trades AS TrainTrades, T.Days AS TrainDays, T.WinRate AS TrainWin, T.BaseWinRate AS TrainBaseWin,
    T.NetR_Uncosted AS TrainNetR, T.PositiveDays AS TrainPosDays,

    V.Trades AS ValidTrades, V.Days AS ValidDays, V.WinRate AS ValidWin, V.BaseWinRate AS ValidBaseWin,
    V.NetR_Uncosted AS ValidNetR, V.PositiveDays AS ValidPosDays,

    CAST(CASE WHEN T.Trades >= @MinTrades AND V.Trades >= @MinTrades
               AND T.Days   >= @MinDays   AND V.Days   >= @MinDays
               AND T.WinRate > T.BaseWinRate AND V.WinRate > V.BaseWinRate
               AND T.NetR_Uncosted > 0 AND V.NetR_Uncosted > 0
              THEN 1 ELSE 0 END AS bit) AS PassBoth
FROM WithBase T
LEFT JOIN WithBase V
  ON V.SplitName = 'VALID' AND V.Direction = T.Direction AND V.Symbol = T.Symbol
 AND V.ComboName = T.ComboName AND V.Condition = T.Condition
WHERE T.SplitName = 'TRAIN'
ORDER BY PassBoth DESC,
         CASE WHEN T.ComboName LIKE 'BASE%' THEN 0 ELSE 1 END DESC,
         T.Direction, T.Symbol, T.ComboName, V.NetR_Uncosted DESC;

/* VERIFY (join integrity): run separately; expect near 100% within tolerance.
SELECT COUNT(*) AS Joined,
       SUM(CASE WHEN ABS(O.EntryPrice - S.Ltp) <= 0.05 THEN 1 ELSE 0 END) AS EntryEqualsLtp
FROM dbo.IndicatorSnapshots S
JOIN dbo.IndicatorSnapshotOutcomes O ON O.IndicatorSnapshotId = S.Id
WHERE S.SnapshotTime >= '2026-09-30 13:30';
*/
