/* ============================================================================
   PROPOSAL — widen the BANKNIFTY PUT 5m RSI band from 28-38 to 32-45
   ----------------------------------------------------------------------------
   PROPOSAL ONLY. Claude does not execute this (.claude/rules/02-approval-gate.md).
   Read the whole header, especially SIDE EFFECT, before running anything.

   Evidence: docs/propose-banknifty-put-rsi-band.md
             docs/research/2026-10-15-put-rsi-band/

   WHAT THIS DOES
   --------------
   Inserts BANKNIFTY-scoped rows for PutRsiFloor and PutRsiCeiling. The live
   lookup order is exact Symbol -> Symbol IS NULL (global) -> compiled default
   (.claude/rules/00-project-map.md), so a BANKNIFTY row overrides the global one
   and NIFTY/SENSEX keep 28/38 untouched. That scoping is deliberate: the
   evidence only supports BANKNIFTY.

   CURRENT STATE (read 2026-10-16)
   -------------------------------
     PutRsiFloor    (global)  28   IsActive=1   Id 4036
     PutRsiCeiling  (global)  38   IsActive=1   Id 4037
     PutPcrMinimum  SENSEX    0.7  IsActive=1   Id 9045
     EnablePutPcrFilter (global) 1 IsActive=1   Id 7024
   No BANKNIFTY rows exist for either RSI key, so this INSERTs rather than
   UPDATEs. The script handles both.

   *** SIDE EFFECT — READ THIS ***
   PutRsiFloor / PutRsiCeiling drive TWO things, not one
   (SignalEngineService.RsiConfidenceBand, lines ~145-149):

     1. the PUT entry band (the intended change), and
     2. the RSI term of the confidence score, which feeds LOT SIZING:

            if (rsi <= PutRsiFloor)        score += 15
            else if (rsi <= PutRsiCeiling) score += 8

   So after this change, for BANKNIFTY PUT:
     - RSI 28-32 scores +15 instead of +8  (was mid-band, now "deep")
     - RSI 38-45 scores  +8 instead of  0  (was outside the band entirely)
   Confidence therefore RISES for those entries, and position size with it.

   THE BACKTEST DID NOT MODEL THIS. ExitModel uses a fixed lot count
   (Math.Max(1, AdaptiveLotMin), currently 4) and nothing in Trading.Backtest
   reads confidence at all. The +35,176 figure is at CONSTANT size. Live, the
   same trades would be taken at LARGER size, so both profit and drawdown scale
   up by an amount this evidence does not measure.

   If that is not wanted, the clean separation is to give the confidence formula
   its own keys so entry and sizing can move independently - a code change, not
   a settings change, and out of scope here.

   SAFETY
   ------
   - Idempotent: re-running changes nothing after the first run.
   - Touches only two StrategySettings rows, both BANKNIFTY-scoped.
   - No schema change, no other symbol, no other key.
   - ROLLBACK and VERIFY blocks at the bottom.
   - Settings are cached ~5 minutes (StrategySettingsService), so the change is
     not instant; it does NOT require a restart.
   ============================================================================ */

SET NOCOUNT ON;
GO

DECLARE @Symbol  nvarchar(50) = N'BANKNIFTY';
DECLARE @Floor   decimal(18,6) = 32.0;   -- was 28 (global)
DECLARE @Ceiling decimal(18,6) = 45.0;   -- was 38 (global)

/* ---------------------------------------------------------------------------
   PRE-FLIGHT. Expect the two global rows at 28/38 and no BANKNIFTY rows.
   --------------------------------------------------------------------------- */
SELECT SettingKey,
       ISNULL(Symbol, N'(global)') AS Scope,
       SettingValue,
       IsActive
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey IN (N'PutRsiFloor', N'PutRsiCeiling')
ORDER BY SettingKey, Symbol;

/* ---------------------------------------------------------------------------
   APPLY — idempotent upsert of the two BANKNIFTY rows.
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = N'PutRsiFloor' AND Symbol = @Symbol)
BEGIN
    UPDATE dbo.StrategySettings
       SET SettingValue = @Floor, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = N'PutRsiFloor' AND Symbol = @Symbol;
    PRINT 'PutRsiFloor (BANKNIFTY) updated';
END
ELSE
BEGIN
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (N'PutRsiFloor', @Symbol, @Floor, 1,
            N'BANKNIFTY PUT 5m RSI floor 32 (was 28 global). Walk-forward validated, costed. See docs/propose-banknifty-put-rsi-band.md',
            SYSDATETIME());
    PRINT 'PutRsiFloor (BANKNIFTY) inserted';
END

IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = N'PutRsiCeiling' AND Symbol = @Symbol)
BEGIN
    UPDATE dbo.StrategySettings
       SET SettingValue = @Ceiling, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = N'PutRsiCeiling' AND Symbol = @Symbol;
    PRINT 'PutRsiCeiling (BANKNIFTY) updated';
END
ELSE
BEGIN
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (N'PutRsiCeiling', @Symbol, @Ceiling, 1,
            N'BANKNIFTY PUT 5m RSI ceiling 45 (was 38 global). Walk-forward validated, costed. See docs/propose-banknifty-put-rsi-band.md',
            SYSDATETIME());
    PRINT 'PutRsiCeiling (BANKNIFTY) inserted';
END
GO

/* ---------------------------------------------------------------------------
   VERIFY. Expect four rows: global 28/38 unchanged, BANKNIFTY 32/45 active.
   --------------------------------------------------------------------------- */
SELECT SettingKey,
       ISNULL(Symbol, N'(global)') AS Scope,
       SettingValue,
       IsActive,
       UpdatedOn
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey IN (N'PutRsiFloor', N'PutRsiCeiling')
ORDER BY SettingKey, Symbol;

/* Confirm no other symbol was touched: this must return 0 rows. */
SELECT COUNT(*) AS OtherSymbolRowsChangedToday
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey IN (N'PutRsiFloor', N'PutRsiCeiling')
  AND ISNULL(Symbol, N'') NOT IN (N'', N'BANKNIFTY')
  AND CONVERT(date, UpdatedOn) = CONVERT(date, SYSDATETIME());

/* ---------------------------------------------------------------------------
   ROLLBACK — deletes the BANKNIFTY rows so the global 28/38 applies again.
   Delete rather than IsActive=0: an inactive row is NOT the same as no row
   (IsRsiConfidenceActive treats IsActive=0 as "drop the RSI confidence term",
   which is a different behaviour from falling back to the global band).
   ---------------------------------------------------------------------------
DELETE FROM dbo.StrategySettings
 WHERE SettingKey IN (N'PutRsiFloor', N'PutRsiCeiling') AND Symbol = N'BANKNIFTY';

SELECT SettingKey, ISNULL(Symbol, N'(global)') AS Scope, SettingValue, IsActive
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey IN (N'PutRsiFloor', N'PutRsiCeiling')
ORDER BY SettingKey, Symbol;        -- expect only the two global rows
   --------------------------------------------------------------------------- */
