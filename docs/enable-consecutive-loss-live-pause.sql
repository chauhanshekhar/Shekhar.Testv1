-- Enable "after N consecutive losses today, route NEW entries to paper (live paused for the day)".
-- NOT executed by Claude. Needs the Worker build that contains ConsecutiveLossLivePauseGate deployed first.
-- Global rows only (Symbol IS NULL). Only matters when TradingMode is Live/HeroZero; no effect in Paper.
-- Settings refresh in the Worker within ~5 minutes.

-- BEFORE
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings WHERE SettingKey IN ('EnableConsecutiveLossLivePause','ConsecutiveLossLivePauseCount');

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'EnableConsecutiveLossLivePause' AND Symbol IS NULL)
    UPDATE StrategySettings SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSUTCDATETIME()
     WHERE SettingKey = 'EnableConsecutiveLossLivePause' AND Symbol IS NULL;
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableConsecutiveLossLivePause', NULL, 1, 1,
            'After N consecutive losses today, new entries are paper-only (live paused for the day)', SYSUTCDATETIME());

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'ConsecutiveLossLivePauseCount' AND Symbol IS NULL)
    UPDATE StrategySettings SET SettingValue = 3, IsActive = 1, UpdatedOn = SYSUTCDATETIME()
     WHERE SettingKey = 'ConsecutiveLossLivePauseCount' AND Symbol IS NULL;
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('ConsecutiveLossLivePauseCount', NULL, 3, 1,
            'Consecutive losses that pause live entries for the rest of the IST day', SYSUTCDATETIME());

-- VERIFY (expect Enable=1, Count=3, both IsActive=1)
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings WHERE SettingKey IN ('EnableConsecutiveLossLivePause','ConsecutiveLossLivePauseCount');

/* ROLLBACK:
UPDATE StrategySettings SET SettingValue = 0, UpdatedOn = SYSUTCDATETIME()
 WHERE SettingKey = 'EnableConsecutiveLossLivePause' AND Symbol IS NULL;
*/
