/*
================================================================================
 PcrFullChainAtEntry on Signals and TradeJournal
================================================================================
Equivalent of EF migration AddPcrFullChainAtEntry. Run this OR `dotnet ef database update`,
not both (the guards make a second run harmless, but EF history would then be out of step).

Apply BEFORE deploying the new Worker: the Worker inserts Signals.PcrFullChainAtEntry.
Nullable: NULL = full-chain indicator stale (>2 min) at entry, or row predates the column.
Claude does not execute this script.
================================================================================
*/

/* 1. ADD COLUMNS */
IF COL_LENGTH('dbo.Signals', 'PcrFullChainAtEntry') IS NULL
    ALTER TABLE dbo.Signals ADD PcrFullChainAtEntry decimal(18,2) NULL;

IF COL_LENGTH('dbo.TradeJournal', 'PcrFullChainAtEntry') IS NULL
    ALTER TABLE dbo.TradeJournal ADD PcrFullChainAtEntry decimal(18,2) NULL;
GO

/* 2. OPTIONAL BACKFILL (history). Latest IndicatorSnapshots.PcrFullChain at or before
      Signals.CreatedOn, within 2 minutes (same freshness rule as live). No look-ahead.
      Only fills NULLs. Snapshots only carry PcrFullChain since 2026-11-07. */
UPDATE s
   SET s.PcrFullChainAtEntry = x.PcrFullChain
  FROM dbo.Signals s
 CROSS APPLY (SELECT TOP (1) i.PcrFullChain
                FROM dbo.IndicatorSnapshots i
               WHERE i.Symbol = s.Symbol
                 AND i.PcrFullChain IS NOT NULL
                 AND i.SnapshotTime <= s.CreatedOn
                 AND i.SnapshotTime >= DATEADD(MINUTE, -2, s.CreatedOn)
               ORDER BY i.SnapshotTime DESC) x
 WHERE s.PcrFullChainAtEntry IS NULL;

UPDATE j
   SET j.PcrFullChainAtEntry = s.PcrFullChainAtEntry
  FROM dbo.TradeJournal j
  JOIN dbo.Signals s ON s.Id = j.SignalId
 WHERE j.PcrFullChainAtEntry IS NULL AND s.PcrFullChainAtEntry IS NOT NULL;
GO

/* ---------------------------------------------------------------------------
   ROLLBACK
   ---------------------------------------------------------------------------
IF COL_LENGTH('dbo.TradeJournal', 'PcrFullChainAtEntry') IS NOT NULL
    ALTER TABLE dbo.TradeJournal DROP COLUMN PcrFullChainAtEntry;
IF COL_LENGTH('dbo.Signals', 'PcrFullChainAtEntry') IS NOT NULL
    ALTER TABLE dbo.Signals DROP COLUMN PcrFullChainAtEntry;
   (If applied via EF, use `dotnet ef database update <previous migration>` instead.)
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   VERIFY
   --------------------------------------------------------------------------- */
SELECT 'Signals' AS TableName, COUNT(*) AS Total, COUNT(PcrFullChainAtEntry) AS WithValue
  FROM dbo.Signals
UNION ALL
SELECT 'TradeJournal', COUNT(*), COUNT(PcrFullChainAtEntry) FROM dbo.TradeJournal;

SELECT TOP (10) Id, Symbol, CreatedOn, PcrAtEntry, PcrFullChainAtEntry
  FROM dbo.Signals ORDER BY Id DESC;
