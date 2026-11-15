-- READ-ONLY. Pasted query's Gap-Down / PUT / RSI(1m) 45-55 / ATR<0.03 / Bearish cell, with diagnostics.
WITH Base AS (
  SELECT O.Symbol, O.FirstHitResult15Min AS R15, S.SnapshotTime, S.Rsi, S.Rsi5m, S.AtrPct, S.Ltp, S.Ema20, S.Ema50, S.Ema20_5m, S.Ema50_5m,
         100.0*(S.DayOpen-S.YesterdayClose)/NULLIF(S.YesterdayClose,0) AS GapPct
  FROM IndicatorSnapshotOutcomes O WITH (NOLOCK)
  JOIN IndicatorSnapshots S WITH (NOLOCK) ON S.Id=O.IndicatorSnapshotId
  WHERE UPPER(O.Direction)='PUT'
), C AS (
  SELECT *, CONVERT(varchar(10),SnapshotTime,120) AS Day, CONVERT(varchar(16),SnapshotTime,120) AS Minute
  FROM Base
  WHERE GapPct<=-0.30 AND Rsi>=45 AND Rsi<55 AND AtrPct<0.03
    AND Ltp<Ema20 AND Ema20<Ema50 AND Ema20_5m<Ema50_5m
)
SELECT Symbol, Day, COUNT(*) AS Rows_, COUNT(DISTINCT Minute) AS Minutes,
  SUM(CASE WHEN R15='TARGET' THEN 1 ELSE 0 END) AS Tgt, SUM(CASE WHEN R15='SL' THEN 1 ELSE 0 END) AS Sl,
  SUM(CASE WHEN R15 IS NULL OR R15 NOT IN ('TARGET','SL') THEN 1 ELSE 0 END) AS Other,
  SUM(CASE WHEN Rsi5m IS NULL THEN 1 ELSE 0 END) AS Rsi5mNull
FROM C GROUP BY Symbol, Day ORDER BY Rows_ DESC;
