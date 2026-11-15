/*
  enable-expiry-auto-roll.sql

  PURPOSE
    Turn on ExpiryRollWorker, which sets TradingInstruments.Expiry to the next option expiry (from
    Groww's instrument CSV) when the stored date is missing or already past. Default is OFF; this row
    is what switches it on. Global row (Symbol NULL), read via StrategySettingsService, cached ~5 min.

  BEFORE RUNNING
    Deploy the Worker build that contains ExpiryRollWorker first; the row does nothing without it.
    Optionally run docs/set-instrument-expiry-dates.sql once so the current dates are right today.
*/

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'EnableExpiryAutoRoll' AND Symbol IS NULL)
    UPDATE StrategySettings
    SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
    WHERE SettingKey = 'EnableExpiryAutoRoll' AND Symbol IS NULL;
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableExpiryAutoRoll', NULL, 1, 1,
            'Roll TradingInstruments.Expiry forward from the Groww instrument CSV when the date is missing or past',
            SYSDATETIME());

/* VERIFY */
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings
WHERE SettingKey = 'EnableExpiryAutoRoll';

SELECT Symbol, Exchange, IsActive, Expiry, UpdatedOn
FROM TradingInstruments
WHERE Symbol IN ('NIFTY','BANKNIFTY','SENSEX')
ORDER BY Symbol;

/* ROLLBACK (switches the worker off; stored dates are left as they are)
   UPDATE StrategySettings SET SettingValue = 0, IsActive = 0, UpdatedOn = SYSDATETIME()
   WHERE SettingKey = 'EnableExpiryAutoRoll' AND Symbol IS NULL;
*/
