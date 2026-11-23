/* =====================================================================================================================
   set-nifty-lot-min.sql
   Sets NIFTY to 1 lot: StrategySettings.AdaptiveLotMin for Symbol = 'NIFTY'.
   AdaptiveLotSizingService trades a FIXED AdaptiveLotMin lots per symbol (per-symbol row -> global row -> appsettings).

   Evidence (read-only, 2026-11-23): there is NO NIFTY or global AdaptiveLotMin row, yet every NIFTY signal 12-23 Nov
   traded 4 lots (e.g. Signal 10418: BUY 260 qty = 4 x 65, -4,245.80 Rs). The 4 comes from the deployed appsettings
   (this checkout's appsettings.json says 1), so a per-symbol row is the only way to pin it from the database.
   NIFTY 12-23 Nov: 6 trades at 4 lots, -6,973 Rs; at 1 lot the same trades would have been about -1,743 Rs.

   NOT executed by Claude - review, then run it yourself in TradingDB_Paper.
   ===================================================================================================================== */
SET XACT_ABORT ON;

DECLARE @Lots    decimal(18, 6) = 1;
DECLARE @Symbol  nvarchar(50)   = 'NIFTY';
DECLARE @Now     datetime2      = DATEADD(MINUTE, 330, SYSUTCDATETIME());   -- IST, as IstClock.Now

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'AdaptiveLotMin' AND Symbol = @Symbol)
    UPDATE StrategySettings
       SET SettingValue = @Lots, IsActive = 1, UpdatedOn = @Now
     WHERE SettingKey = 'AdaptiveLotMin' AND Symbol = @Symbol;
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('AdaptiveLotMin', @Symbol, @Lots, 1,
            'Fixed lots per entry for NIFTY (AdaptiveLotSizingService). Overrides appsettings AdaptiveLotMin.',
            @Now);

COMMIT TRANSACTION;

/* ---- VERIFY ---------------------------------------------------------------------------------------------------------
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings WHERE SettingKey = 'AdaptiveLotMin' ORDER BY Symbol;
-- expect NIFTY = 1.000000, IsActive = 1 (BANKNIFTY 2 and SENSEX 2 unchanged).
-- StrategySettingsService caches ~5 minutes; the next NIFTY entry after that should show Orders.Quantity = 1 (65 units).
------------------------------------------------------------------------------------------------------------------------ */

/* ---- ROLLBACK -------------------------------------------------------------------------------------------------------
-- Before this script there was NO NIFTY row; NIFTY traded the deployed appsettings value (4 lots observed).
-- Restore that effective value without deleting anything:
UPDATE StrategySettings SET SettingValue = 4, UpdatedOn = DATEADD(MINUTE, 330, SYSUTCDATETIME())
WHERE SettingKey = 'AdaptiveLotMin' AND Symbol = 'NIFTY';
-- (or set IsActive = 0 on the row to fall back to appsettings again)
------------------------------------------------------------------------------------------------------------------------ */
