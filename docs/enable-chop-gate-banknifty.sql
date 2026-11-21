-- Enable ChopGate for BANKNIFTY (currently an exact-symbol row = 0 overrides the global row = 1).
-- NOT executed by Claude. Run manually against the intended database (TradingDB_Paper first).
-- Evidence note: ChopGate is an unvalidated, SENSEX-derived hypothesis (see ChopGate.cs header).
-- On 2026-11-18 BANKNIFTY chop at entry was 2.3-4.4 (< 4.5), so this would NOT have blocked any
-- of that day's BANKNIFTY trades. Settings refresh in the Worker within ~5 minutes (no restart).

-- BEFORE
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings WHERE SettingKey = 'EnableChopGate';

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'EnableChopGate' AND Symbol = 'BANKNIFTY')
    UPDATE StrategySettings
       SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSUTCDATETIME()
     WHERE SettingKey = 'EnableChopGate' AND Symbol = 'BANKNIFTY';
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableChopGate', 'BANKNIFTY', 1, 1,
            'Chop gate enabled for BANKNIFTY (forward trial)', SYSUTCDATETIME());

-- VERIFY (expect BANKNIFTY row SettingValue = 1, IsActive = 1)
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings WHERE SettingKey = 'EnableChopGate';

/* ROLLBACK (restore the previous BANKNIFTY kill-switch):
UPDATE StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSUTCDATETIME()
 WHERE SettingKey = 'EnableChopGate' AND Symbol = 'BANKNIFTY';
*/
