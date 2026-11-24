/* =====================================================================================================================
   enable-cross-index-gate.sql
   Makes the CrossIndexAgreementGate configuration explicit and active (global rows):
     EnableCrossIndexAgreementGate = 1   (already 1 and active in the DB on 2026-11-24; this keeps it that way)
     CrossIndexLookbackMinutes     = 15  (was inactive -> the code default 15 applied)
     CrossIndexMinAgree            = 2   (was inactive -> the code default 2 applied)
     CrossIndexAgreementSide       = 0   (was inactive -> the code default 0 = CALL and PUT applied)
   Behaviour does not change: the activated values equal the defaults that were already in force.
   Change @Side to 1 (CALL only) or 2 (PUT only) to limit the gate to one direction.
   The gate compares the OTHER ACTIVE indices (TradingInstruments.IsActive): with NIFTY inactive, BANKNIFTY and SENSEX are each
   compared with the one other active index. Replay 2026-11-24 with the gate: 147 trades, net +44,195, PF 1.43 (without it
   155 trades, +42,580, PF 1.38). The replay uses today's active instruments for the whole history.
   NOT executed by Claude - review, then run it yourself.
   ===================================================================================================================== */
SET XACT_ABORT ON;

DECLARE @Lookback int = 15;     -- minutes of closed 1m bars compared
DECLARE @MinAgree int = 2;      -- how many of the other indices must agree (capped at how many have data)
DECLARE @Side     int = 0;      -- 0 = CALL and PUT, 1 = CALL only, 2 = PUT only
DECLARE @Now datetime2 = DATEADD(MINUTE, 330, SYSUTCDATETIME());   -- IST, as IstClock.Now

BEGIN TRANSACTION;

DECLARE @Rows TABLE (SettingKey nvarchar(100), Value decimal(18, 6), Descr nvarchar(500));
INSERT INTO @Rows VALUES
 ('EnableCrossIndexAgreementGate', 1,         'CrossIndexAgreementGate: other indices must move in the trade direction. 1 = on, 0 = off.'),
 ('CrossIndexLookbackMinutes',     @Lookback, 'CrossIndexAgreementGate: minutes of closed 1m bars compared.'),
 ('CrossIndexMinAgree',            @MinAgree, 'CrossIndexAgreementGate: how many other indices must agree (2 = all of them).'),
 ('CrossIndexAgreementSide',       @Side,     'CrossIndexAgreementGate side: 0 = CALL and PUT, 1 = CALL only, 2 = PUT only.');

UPDATE s SET SettingValue = r.Value, IsActive = 1, UpdatedOn = @Now
  FROM StrategySettings s JOIN @Rows r ON r.SettingKey = s.SettingKey
 WHERE s.Symbol IS NULL;

INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
SELECT r.SettingKey, NULL, r.Value, 1, r.Descr, @Now
  FROM @Rows r
 WHERE NOT EXISTS (SELECT 1 FROM StrategySettings s WHERE s.SettingKey = r.SettingKey AND s.Symbol IS NULL);

COMMIT TRANSACTION;

/* ---- VERIFY ---------------------------------------------------------------------------------------------------------
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn FROM StrategySettings
 WHERE SettingKey IN ('EnableCrossIndexAgreementGate', 'CrossIndexLookbackMinutes', 'CrossIndexMinAgree', 'CrossIndexAgreementSide')
 ORDER BY SettingKey, Symbol;     -- expect four global rows, IsActive = 1, values 1 / 15 / 2 / 0 (or your @Side)
------------------------------------------------------------------------------------------------------------------------ */

/* ---- ROLLBACK (back to the state of 2026-11-24: gate on, the three parameter rows inactive, so code defaults apply) ----
UPDATE StrategySettings SET IsActive = 0, UpdatedOn = DATEADD(MINUTE, 330, SYSUTCDATETIME())
 WHERE Symbol IS NULL AND SettingKey IN ('CrossIndexLookbackMinutes', 'CrossIndexMinAgree', 'CrossIndexAgreementSide');
-- to switch the gate OFF entirely instead:
-- UPDATE StrategySettings SET SettingValue = 0, UpdatedOn = DATEADD(MINUTE, 330, SYSUTCDATETIME())
--  WHERE Symbol IS NULL AND SettingKey = 'EnableCrossIndexAgreementGate';
------------------------------------------------------------------------------------------------------------------------ */
