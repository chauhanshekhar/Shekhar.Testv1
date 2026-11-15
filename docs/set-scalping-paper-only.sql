-- ScalpingPaperOnly: Scalping- and Reversal-state signals are paper-only even in Live/HeroZero mode.
-- Code default is ON (1) when the row is absent, so running this script is OPTIONAL; use it to make the
-- switch visible/editable or to turn it OFF. Global row (Symbol IS NULL). Cached ~5 min by the app.
-- NOT executed by Claude - run manually.

DECLARE @Key nvarchar(100) = N'ScalpingPaperOnly';
DECLARE @Value decimal(18,4) = 1;   -- 1 = scalping is paper-only, 0 = scalping follows the global mode

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = @Key AND Symbol IS NULL)
    UPDATE StrategySettings
       SET SettingValue = @Value, IsActive = 1, UpdatedOn = SYSUTCDATETIME()
     WHERE SettingKey = @Key AND Symbol IS NULL;
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (@Key, NULL, @Value, 1, N'Scalping and Reversal market-state signals are paper-only (1) or follow global mode (0)', SYSUTCDATETIME());

-- VERIFY
SELECT SettingKey, Symbol, SettingValue, IsActive FROM StrategySettings WHERE SettingKey = N'ScalpingPaperOnly';

-- ROLLBACK (removes the row; code default of ON then applies. To turn OFF instead, set @Value = 0 above.)
-- DELETE FROM StrategySettings WHERE SettingKey = N'ScalpingPaperOnly' AND Symbol IS NULL;
