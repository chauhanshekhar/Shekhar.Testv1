-- Enable T2 partial exit (EnableT2Partial) for one symbol.
-- 3+ lots: T1 sells half, T2 sells half of the rest, the last lot trails (stops / 15:15 force exit).
-- 1-2 lot trades are unchanged. Ignored while EnableTargetLadder is on for the symbol.
-- Code must be deployed first; before that the row is simply unread. Run by the user - not by Claude.
-- Default is OFF (no row = off). @Symbol = NULL writes the GLOBAL row (all symbols); a per-symbol row, if one exists, wins over it.
-- Set @Symbol to NIFTY / BANKNIFTY / SENSEX for a single symbol instead.
DECLARE @Symbol nvarchar(50) = NULL;               -- NULL = global (all symbols)
DECLARE @Key    nvarchar(100) = N'EnableT2Partial';

-- PRE-CHECK 1: any EnableTargetLadder row that is on makes the ladder win over T2 partial for that symbol.
SELECT SettingKey, Symbol, SettingValue, IsActive FROM StrategySettings WHERE SettingKey IN (N'EnableTargetLadder', N'EnableT2Partial');
-- PRE-CHECK 2: it only acts on trades with 3+ lots. Lots per symbol come from AdaptiveLotMin / the lots settings (see docs/diagnose-lots-per-symbol.sql).

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = @Key AND ((Symbol = @Symbol) OR (Symbol IS NULL AND @Symbol IS NULL)))
    UPDATE StrategySettings
       SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSUTCDATETIME()
     WHERE SettingKey = @Key AND ((Symbol = @Symbol) OR (Symbol IS NULL AND @Symbol IS NULL));
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (@Key, @Symbol, 1, 1, N'T1 sells half, T2 sells half of the rest, last lot trails (3+ lots)', SYSUTCDATETIME());

-- VERIFY
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn FROM StrategySettings WHERE SettingKey = @Key;

-- ROLLBACK (back to legacy: T2 = full exit)
-- UPDATE StrategySettings SET SettingValue = 0, UpdatedOn = SYSUTCDATETIME()
--  WHERE SettingKey = N'EnableT2Partial' AND ((Symbol = @Symbol) OR (Symbol IS NULL AND @Symbol IS NULL));
