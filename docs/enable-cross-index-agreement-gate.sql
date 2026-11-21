-- Enable CrossIndexAgreementGate: take a CALL only when the OTHER indices moved up over the last 15 closed 1m bars,
-- a PUT only when they moved down. Post-signal gate; it only blocks, never adds a trade. HeroZero is exempt.
-- NOT executed by Claude. Run it yourself, ideally while TradingMode is Paper (the gate blocks in every mode; there
-- is no paper-only switch), and re-run docs/research/2026-10-26-cross-index-agreement on the new signals after ~10 sessions.
-- Global row only (Symbol IS NULL). The Worker picks it up within ~5 minutes; no redeploy is needed because
-- CrossIndexAgreementGate is already registered in SignalWorker.PostGates.
--
-- Evidence (see the gate's summary): 263 closed signals 2026-08-04..10-26, stop rate 35% vs 44%, expectancy +0.27R vs -0.12R;
-- ~15 cells searched, first half only break-even after costs. A loss avoider, not a proven entry edge.
-- Replay of 2026-11-18: would have blocked 6 of 15 live trades (-5,103 of -13,706), uncosted, one day.
--
-- The existing row has SettingValue = 0 AND IsActive = 0. IsActive = 0 means "setting does not apply", so BOTH must change.

-- BEFORE
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings
WHERE SettingKey IN ('EnableCrossIndexAgreementGate','CrossIndexLookbackMinutes','CrossIndexMinAgree','CrossIndexAgreementSide')
ORDER BY SettingKey, Symbol;

IF EXISTS (SELECT 1 FROM StrategySettings WHERE SettingKey = 'EnableCrossIndexAgreementGate' AND Symbol IS NULL)
    UPDATE StrategySettings SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSUTCDATETIME()
     WHERE SettingKey = 'EnableCrossIndexAgreementGate' AND Symbol IS NULL;
ELSE
    INSERT INTO StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableCrossIndexAgreementGate', NULL, 1, 1,
            'Entry needs the other indices to have moved in the trade direction over the lookback (blocks only)', SYSUTCDATETIME());

-- Tuning rows (CrossIndexLookbackMinutes 15, CrossIndexMinAgree 2 = all other indices, CrossIndexAgreementSide 0 = CALL and PUT)
-- are not touched: the compiled defaults already match the researched setting. Insert them only to change a value.

-- VERIFY (expect Enable = 1 and IsActive = 1 for the Symbol IS NULL row; no per-symbol row overriding it)
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM StrategySettings
WHERE SettingKey IN ('EnableCrossIndexAgreementGate','CrossIndexLookbackMinutes','CrossIndexMinAgree','CrossIndexAgreementSide')
ORDER BY SettingKey, Symbol;

/* ROLLBACK (restores the original state: value 0, inactive):
UPDATE StrategySettings SET SettingValue = 0, IsActive = 0, UpdatedOn = SYSUTCDATETIME()
 WHERE SettingKey = 'EnableCrossIndexAgreementGate' AND Symbol IS NULL;
*/
