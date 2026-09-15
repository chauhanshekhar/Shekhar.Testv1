/*
================================================================================
 StrategySettings — rows for the new CALL/PUT entry specification
================================================================================
IDEMPOTENT. Inserts a row only when that key does not already exist, so re-running
is safe and any value you have since tuned by hand is left alone.

SAFE TO RUN BEFORE DEPLOY. Every key below is read only by the NEW code. The
currently-deployed build ignores all of them, so this script changes nothing
until the new Worker image is running.

ORPHAN CLEANUP IS DELIBERATELY *NOT* IN THIS SCRIPT — see the notes at the end.
Deactivating a row makes the running code fall back to its COMPILED default, and
for one of the orphans that default is the opposite of the stored value.
================================================================================
*/

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;   -- StrategySettings has a filtered index; INSERT requires this

DECLARE @new TABLE (SettingKey varchar(100), Val decimal(18,6), Descr varchar(300));

-- ── CALL specification ──────────────────────────────────────────────────────
INSERT INTO @new VALUES
 ('CallRsiFloor',                52,     'CALL cond 5: RSI lower bound'),
 ('CallRsiCeiling',              70,     'CALL cond 5: RSI upper bound'),
 ('CallBodyPctMinimum',          30,     'CALL cond 7: candle body % of range'),
 ('CallMinAtrPercent',           0.03,   'CALL cond 9: ATR% floor'),
 ('CallDeltaMinimum',            0.35,   'CALL cond 10: CE delta lower bound'),
 ('CallDeltaMaximum',            0.65,   'CALL cond 10: CE delta upper bound'),
 ('CallDayChangePctMin',         0.10,   'CALL cond 11: DayChange% vs prev close'),
 ('CallRecoveryPctMin',          0.40,   'CALL cond 11: recovery from day low'),
 ('EnableCallVwapFilter',        0,      'CALL cond 3 - OFF: index VWAP is always NULL'),
 ('EnableCallVolumeFilter',      0,      'CALL cond 8 - OFF: index candle volume is always 0'),
 ('EnableCallPrev5mHighFilter',  1,      'CALL cond 4: break of prev closed 5m high'),
 ('EnableCallDayFilter',         1,      'CALL cond 11'),
 ('EnableCall5mTrendFilter',     0,      'CALL retired: 5m EMA alignment'),
 ('EnableCallPriceChangeFilter', 0,      'CALL retired: PriceChangePct vs day open');

-- ── PUT specification ───────────────────────────────────────────────────────
INSERT INTO @new VALUES
 ('PutRsiFloor',                 30,     'PUT cond 5: RSI lower bound'),
 ('PutRsiCeiling',               48,     'PUT cond 5: RSI upper bound (was RsiPutCeiling=55)'),
 ('PutBodyPctMinimum',           30,     'PUT cond 7: candle body % of range'),
 ('PutMinAtrPercent',            0.03,   'PUT cond 9: ATR% floor'),
 ('PutDeltaMinimum',             0.35,   'PUT cond 10: |PE delta| lower bound'),
 ('PutDeltaMaximum',             0.65,   'PUT cond 10: |PE delta| upper bound'),
 ('PutDayChangePctMax',         -0.10,   'PUT cond 11: DayChange% vs prev close'),
 ('PutFallPctMin',               0.40,   'PUT cond 11: fall from day high'),
 ('EnablePutVwapFilter',         0,      'PUT cond 3 - OFF: index VWAP is always NULL'),
 ('EnablePutVolumeFilter',       0,      'PUT cond 8 - OFF: index candle volume is always 0'),
 ('EnablePutPrev5mLowFilter',    1,      'PUT cond 4: break of prev closed 5m low'),
 ('EnablePutDayFilter',          1,      'PUT cond 11'),
 ('EnablePut5mTrendFilter',      0,      'PUT retired: 5m EMA alignment'),
 ('EnablePutPriceChangeFilter',  0,      'PUT retired: PriceChangePct vs day open'),
 ('EnablePutPcrFilter',          0,      'PUT retired: PCR gate - WAS LIVE before this spec');

-- ── Rule kill switch ────────────────────────────────────────────────────────
INSERT INTO @new VALUES
 ('EnableBankNiftyPutBreakdownRule', 1,  'BANKNIFTY PUT BREAKDOWN on/off (no redeploy needed)');

INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
SELECT n.SettingKey, NULL, n.Val, 1, n.Descr, SYSDATETIME()
FROM @new n
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.StrategySettings s
    WHERE s.SettingKey = n.SettingKey AND s.Symbol IS NULL);

PRINT CONCAT('Rows inserted: ', @@ROWCOUNT);

-- Verify
SELECT SettingKey, ISNULL(Symbol,'(global)') AS Sym, SettingValue, IsActive
FROM dbo.StrategySettings
WHERE SettingKey IN (SELECT SettingKey FROM @new)
ORDER BY SettingKey;


/*
================================================================================
 ORPHANED ROWS — do NOT deactivate until the new build is deployed
================================================================================
These keys are no longer read by any code after the CALL/PUT rewrite:

    DeltaMinimum                  EnableCallStrongTrendFilter
    EnableCallPcrFilter           EnablePcrFilter
    EnablePriceChangeFilter       PcrBullishThreshold
    EnableSensexDirectionalRule   FixedIndexPoints

Deactivating a row does not neutralise it — StrategySettingsService filters on
IsActive, so the caller falls back to its COMPILED default. For most of these the
stored value already equals the default, so cleanup is harmless:

    DeltaMinimum                 0.35 = default 0.35      safe
    EnableCallPcrFilter          0    = default false     safe
    EnableCallStrongTrendFilter  1    = default true      safe
    EnablePcrFilter              1    = default true      safe
    PcrBullishThreshold          1.00 = default 1.00      safe

    EnablePriceChangeFilter      0    but default is TRUE  <-- NOT SAFE

Deactivating EnablePriceChangeFilter would switch a filter ON in the currently
running build that is deliberately off today. Leave every orphan in place until
the new image is live; afterwards they are inert and can be removed together:

    UPDATE dbo.StrategySettings SET IsActive = 0, UpdatedOn = SYSDATETIME()
    WHERE SettingKey IN ('DeltaMinimum','EnableCallPcrFilter',
                         'EnableCallStrongTrendFilter','EnablePcrFilter',
                         'EnablePriceChangeFilter','PcrBullishThreshold',
                         'EnableSensexDirectionalRule','FixedIndexPoints');
================================================================================
*/
