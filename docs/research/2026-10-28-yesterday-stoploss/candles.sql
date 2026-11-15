-- Read-only. 1-minute index candles for 2026-10-27 (CandleTime is UTC).
SELECT c.Symbol, CONVERT(varchar(19), DATEADD(minute,330,c.CandleTime),120) AS T, c.[Open] AS O, c.High AS H, c.Low AS L, c.[Close] AS C
FROM Candles c WITH (NOLOCK)
WHERE c.Symbol IN ('BANKNIFTY_1m','SENSEX_1m')
  AND c.CandleTime >= '2026-10-27 06:30' AND c.CandleTime < '2026-10-27 09:45'
ORDER BY c.Symbol, c.CandleTime;
