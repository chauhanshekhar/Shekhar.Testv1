-- READ-ONLY. Candidate 1 through the (known-suspect) IndicatorSnapshotOutcomes id join.
-- MatchedEntry = outcome.EntryPrice equals snapshot.Ltp (the validity test from memory).
SELECT o.Symbol, CONVERT(varchar(10), s.SnapshotTime, 120) AS Day, CONVERT(varchar(19), s.SnapshotTime, 120) AS SnapTime,
       o.Direction, o.FirstHitResult15Min AS Hit15, o.FirstHitResult10Min AS Hit10, o.FirstHitResult5Min AS Hit5,
       CASE WHEN ABS(o.EntryPrice - s.Ltp) < 0.01 THEN 1 ELSE 0 END AS MatchedEntry,
       CASE WHEN o.Symbol = s.Symbol THEN 1 ELSE 0 END AS SameSymbol,
       o.IndicatorSnapshotId, s.Rsi5m, s.AtrPct, s.Ltp, o.EntryPrice
FROM IndicatorSnapshotOutcomes o WITH (NOLOCK)
JOIN IndicatorSnapshots s WITH (NOLOCK) ON s.Id = o.IndicatorSnapshotId
WHERE o.Direction = 'PUT'
  AND s.DayOpen > 0 AND s.YesterdayClose > 0
  AND (s.DayOpen - s.YesterdayClose) / s.YesterdayClose * 100 <= -0.30
  AND s.Rsi5m >= 45 AND s.Rsi5m < 55 AND s.AtrPct < 0.03
  AND s.Ema20 < s.Ema50 AND s.Ema20_5m < s.Ema50_5m;
