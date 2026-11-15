-- READ-ONLY. 1m index candles (CandleTime is UTC).
SELECT Symbol, CONVERT(varchar(19), CandleTime, 120) AS CandleTime, [High], [Low], [Close]
FROM Candles WITH (NOLOCK)
WHERE Symbol IN ('NIFTY_1m','BANKNIFTY_1m','SENSEX_1m') AND CandleTime >= '2026-07-09'
ORDER BY Symbol, CandleTime;
