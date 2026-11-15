/*
 Switch the chop gate OFF for BANKNIFTY only (per-symbol row overrides the global EnableChopGate row).
 Evidence: docs/research/2026-11-13-chop-gate-nifty-banknifty/ranking.md - blocked BANKNIFTY trades were +12,950 (PF 1.90), both halves.
 Picked up within ~5 min. Executed by the user, never by Claude.
*/
IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EnableChopGate' AND Symbol = 'BANKNIFTY')
    UPDATE dbo.StrategySettings SET SettingValue = 0, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableChopGate' AND Symbol = 'BANKNIFTY';
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableChopGate', 'BANKNIFTY', 0, 1, 'Chop gate OFF for BANKNIFTY (blocked trades were net profitable; overrides global).', SYSDATETIME());

/* ROLLBACK (let the global row apply again)
DELETE FROM dbo.StrategySettings WHERE SettingKey = 'EnableChopGate' AND Symbol = 'BANKNIFTY';
*/

/* VERIFY */
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn FROM dbo.StrategySettings WHERE SettingKey = 'EnableChopGate';
