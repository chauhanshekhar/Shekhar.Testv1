/* Adds IndicatorSnapshots.Ema9 / Ema21 (1m EMA, audit only). Equivalent to EF migration
   20261104021647_AddIndicatorSnapshotEma9Ema21. Idempotent. NOT executed by Claude - run it yourself,
   or apply the migration (dotnet ef database update) BEFORE starting the new Worker. */

-- APPLY
IF COL_LENGTH('dbo.IndicatorSnapshots', 'Ema9') IS NULL
    ALTER TABLE dbo.IndicatorSnapshots ADD Ema9 decimal(18,4) NULL;
IF COL_LENGTH('dbo.IndicatorSnapshots', 'Ema21') IS NULL
    ALTER TABLE dbo.IndicatorSnapshots ADD Ema21 decimal(18,4) NULL;

/* ROLLBACK (stop the new Worker first; it writes these columns)
IF COL_LENGTH('dbo.IndicatorSnapshots', 'Ema9') IS NOT NULL
    ALTER TABLE dbo.IndicatorSnapshots DROP COLUMN Ema9;
IF COL_LENGTH('dbo.IndicatorSnapshots', 'Ema21') IS NOT NULL
    ALTER TABLE dbo.IndicatorSnapshots DROP COLUMN Ema21;
*/

-- VERIFY: columns exist, and after the new Worker has run, recent rows carry values (old rows stay NULL).
SELECT COLUMN_NAME, DATA_TYPE, NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'IndicatorSnapshots' AND COLUMN_NAME IN ('Ema9', 'Ema21');

SELECT TOP (10) SnapshotTime, Symbol, Ema9, Ema21, Ema20, Ema50
FROM dbo.IndicatorSnapshots
ORDER BY Id DESC;
