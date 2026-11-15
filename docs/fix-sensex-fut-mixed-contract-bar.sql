-- Remove the stray SENSEX December-contract bar that poisoned SENSEX_FUT_1m on 2026-10-26.
-- Cause: HistoricalDataWorker.SyncFuturesAsync fell back to SENSEX26DECFUT when SENSEX26NOVFUT returned no candles for
-- one refresh window, and upserted those bars into the same series. Its running volume (560) is below the November
-- series (~7,940), so FuturesFlow.From saw a falling volume series and dropped the VWAP for the rest of the day.
-- Evidence: 11:49 IST Close 74181.05 / Volume 560 vs neighbours 73,720-73,770 / 7,940-7,960.
-- Run on TradingDB_Paper. Run section 1 first and check it returns only the 11:49 row before running section 2.

-- 1) PREVIEW: bars on 2026-10-26 whose running volume is below the preceding bar's (the mixed-contract rows)
;WITH b AS (
  SELECT Id, CandleTime, [Close], Volume,
         LAG(Volume) OVER (ORDER BY CandleTime) pv
  FROM Candles
  WHERE Symbol = 'SENSEX_FUT_1m'
    AND CandleTime >= DATEADD(MINUTE,-330,'2026-10-26 09:15') AND CandleTime < DATEADD(MINUTE,-330,'2026-10-27 00:00'))
SELECT Id, DATEADD(MINUTE,330,CandleTime) t_ist, [Close], Volume, pv FROM b WHERE Volume < pv;

-- 2) FIX: keep a copy, then delete the stray bar (idempotent - re-running deletes nothing)
-- The backup is created with SELECT INTO (copies the identity values as data; a separate INSERT ... SELECT * into an
-- identity table fails with Msg 8101). The delete only runs if the backup holds the row, so it is never lost.
IF OBJECT_ID('dbo.Candles_SensexFutMixedBackup_20261026') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM dbo.Candles_SensexFutMixedBackup_20261026)
    DROP TABLE dbo.Candles_SensexFutMixedBackup_20261026;   -- the empty table left by the first, failed run
GO

IF OBJECT_ID('dbo.Candles_SensexFutMixedBackup_20261026') IS NULL
    SELECT c.* INTO dbo.Candles_SensexFutMixedBackup_20261026
    FROM Candles c
    WHERE c.Symbol = 'SENSEX_FUT_1m'
      AND c.CandleTime = DATEADD(MINUTE,-330,'2026-10-26 11:49')
      AND c.Volume = 560;
GO

DELETE c FROM Candles c
WHERE c.Symbol = 'SENSEX_FUT_1m'
  AND c.CandleTime = DATEADD(MINUTE,-330,'2026-10-26 11:49')
  AND c.Volume = 560
  AND EXISTS (SELECT 1 FROM dbo.Candles_SensexFutMixedBackup_20261026 x WHERE x.Id = c.Id);
GO

-- ROLLBACK (re-insert the saved row; the explicit Id needs IDENTITY_INSERT and a column list - list the columns of
-- Candles from sp_help 'Candles'; or omit Id from the list to let SQL Server assign a new one):
-- SET IDENTITY_INSERT Candles ON;
-- INSERT INTO Candles (Id, <other columns>) SELECT Id, <other columns> FROM dbo.Candles_SensexFutMixedBackup_20261026;
-- SET IDENTITY_INSERT Candles OFF;

-- VERIFY: expect 0 rows (no falls left today)
;WITH b AS (
  SELECT CandleTime, Volume, LAG(Volume) OVER (ORDER BY CandleTime) pv
  FROM Candles
  WHERE Symbol = 'SENSEX_FUT_1m'
    AND CandleTime >= DATEADD(MINUTE,-330,'2026-10-26 09:15') AND CandleTime < DATEADD(MINUTE,-330,'2026-10-27 00:00'))
SELECT * FROM b WHERE Volume < pv;
-- Note: snapshots already stored with Vwap NULL since 11:49 stay NULL (history is not rewritten); new snapshots get a
-- VWAP again once the stray bar is gone and the worker does not re-insert it (see the code fix).
