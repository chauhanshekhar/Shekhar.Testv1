/*
 CHOP GATE - ONE setting for all symbols.
 Keep only the global row (Symbol IS NULL). Remove the SENSEX-specific row, otherwise setting the global row to 0
 would still leave SENSEX on (exact-symbol row wins over the global row).
 NOTE: BANKNIFTY backtest showed the gate blocks net-profitable trades (+12,950, PF 1.90) -
 docs/research/2026-11-13-chop-gate-nifty-banknifty/ranking.md. Single switch means BANKNIFTY is gated too.
 Executed by the user, never by Claude.
*/

/* 1. Global row on (same as docs/enable-chop-gate-all-symbols.sql) */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EnableChopGate' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableChopGate' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableChopGate', NULL, 1, 1, 'All symbols: block entry when 15-bar 1m chop > 4.5 (forward trial). Mode-agnostic. 0 = off.', SYSDATETIME());

/* 2. Remove the redundant SENSEX override so the global row is the only switch */
DELETE FROM dbo.StrategySettings WHERE SettingKey = 'EnableChopGate' AND Symbol = 'SENSEX';

/* ROLLBACK (restore the SENSEX row)
INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
VALUES ('EnableChopGate', 'SENSEX', 1, 1, 'SENSEX only: block entry when 15-bar 1m chop > 4.5 (forward trial). Mode-agnostic. 0 = off.', SYSDATETIME());
*/

/* VERIFY: exactly one row, Symbol NULL */
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn FROM dbo.StrategySettings WHERE SettingKey = 'EnableChopGate';
