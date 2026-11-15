-- =============================================================================
-- BANKNIFTY: 2 lots per entry (NIFTY / SENSEX keep appsettings AdaptiveLotMin = 4)
--
-- Read by AdaptiveLotSizingService (StrategySettings AdaptiveLotMin, per symbol). Inert until a
-- Worker build containing that change is deployed; then picked up within ~5 min (settings cache).
-- =============================================================================

-- PRE-CHECK: any existing AdaptiveLotMin rows. A GLOBAL row (Symbol IS NULL) would also resize
-- NIFTY/SENSEX once the new Worker is deployed - review it before running.
SELECT Id, SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings
WHERE SettingKey = 'AdaptiveLotMin';

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'AdaptiveLotMin' AND Symbol = 'BANKNIFTY')
    UPDATE StrategySettings
    SET SettingValue = 2, IsActive = 1, UpdatedOn = SYSDATETIME(),
        Description = 'Lots per entry for BANKNIFTY (overrides appsettings AdaptiveLotMin)'
    WHERE SettingKey = 'AdaptiveLotMin' AND Symbol = 'BANKNIFTY';
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('AdaptiveLotMin', 'BANKNIFTY', 2, 1,
            'Lots per entry for BANKNIFTY (overrides appsettings AdaptiveLotMin)', SYSDATETIME());

COMMIT TRANSACTION;

-- VERIFY: expect one BANKNIFTY row, SettingValue 2, IsActive 1
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings
WHERE SettingKey = 'AdaptiveLotMin';

-- VERIFY after deploy: new BANKNIFTY signals show Lots = 2, orders 60 contracts
-- SELECT TOP 5 s.Id, s.Symbol, s.CreatedOn, s.Quantity AS Lots, o.Quantity, o.FilledQty
-- FROM Signals s LEFT JOIN Orders o ON o.SignalId = s.Id
-- WHERE s.Symbol = 'BANKNIFTY' ORDER BY s.CreatedOn DESC;

-- =============================================================================
-- ROLLBACK (back to appsettings 4 lots). Do NOT use IsActive = 0 for other numeric keys
-- without checking; here an inactive row falls back to the appsettings value, which is intended.
-- =============================================================================
-- UPDATE StrategySettings SET IsActive = 0, UpdatedOn = SYSDATETIME()
-- WHERE SettingKey = 'AdaptiveLotMin' AND Symbol = 'BANKNIFTY';
