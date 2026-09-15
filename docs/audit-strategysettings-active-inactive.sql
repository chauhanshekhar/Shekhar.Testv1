/*
================================================================================
StrategySettings audit — what exists, what's active/inactive, what's missing
================================================================================
Every key below is read somewhere in Trading.SignalEngine / Trading.Worker via
IStrategySettingsService.{GetDecimal,GetBool,GetInt,IsSettingActive}. Resolution
order for any key+symbol: exact Symbol row -> Symbol IS NULL (global) row ->
the compiled default at the call site. A row with IsActive=0 does NOT fall
through to the global row for that symbol - see StrategySettingsService.cs's
class doc comment for the full IsActive semantics (numeric vs boolean keys
behave differently).

Run section 1 to see everything actually in the table. Run section 2 to see,
for every key the code knows about, whether it's configured at all, and for
configured keys, how many rows are active vs inactive. Section 2 is the one
that answers "which are missing / active / inactive" directly.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

/* ── 1. Raw inventory — everything currently in the table ───────────────────── */

SELECT Id, SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn
FROM   dbo.StrategySettings
ORDER  BY SettingKey, ISNULL(Symbol, ''), IsActive DESC;


/* ── 2. Every key the code reads, cross-checked against what's configured ───── */

;WITH KnownKeys AS (
    SELECT * FROM (VALUES
        -- Global pre-filters / cross-cutting
        ('MinAtrPercent',              'entry-gate  (global ATR pre-filter, SignalEngineService + AtrGate)'),
        ('EnableAtrFilter',            'enable-flag (pairs with MinAtrPercent, AtrGate)'),
        ('MinDirectionalConfidence',   'entry-gate  (StrategyManager confidence floor, Directional only)'),
        ('RejectedEntryCooldownMinutes','entry-gate (DuplicateSignalGate)'),
        ('UseOneMinuteEntryCandle',    'behaviour-flag (StrategyManager trigger candle timeframe)'),
        ('UseFiveMinuteConfirmation',  'behaviour-flag (FiveMinuteConfirmationService, StrategyManager)'),
        ('EnableOutcomeTracking',      'behaviour-flag (IndicatorSnapshotOutcomeWorker)'),

        -- Confidence-only (ComputeConfidence) — legacy bands, NOT the same keys as the entry chain
        ('BodyPctMinimum',             'confidence-only (candle-quality bonus; keeps default-fallback on IsActive=0)'),
        ('RsiCallFloor',               'confidence-only (RSI bonus, CALL)'),
        ('RsiCallCeiling',             'confidence-only (RSI bonus, CALL)'),
        ('RsiPutFloor',                'confidence-only (RSI bonus, PUT)'),
        ('RsiPutCeiling',              'confidence-only (RSI bonus, PUT)'),

        -- CALL entry chain (CallEntryEvaluator)
        ('CallRsiFloor',               'entry-gate CALL cond.5'),
        ('CallRsiCeiling',             'entry-gate CALL cond.5'),
        ('RsiCallPullbackFloor',       'entry-gate CALL cond.5 (pullback path)'),
        ('RsiCallPullbackCeiling',     'entry-gate CALL cond.5 (pullback path)'),
        ('CallBodyPctMinimum',         'entry-gate CALL cond.6+7'),
        ('CallMinAtrPercent',          'entry-gate CALL cond.9'),
        ('CallDeltaMinimum',           'entry-gate CALL cond.10'),
        ('CallDeltaMaximum',           'entry-gate CALL cond.10'),
        ('CallDayChangePctMin',        'entry-gate CALL cond.11'),
        ('CallRecoveryPctMin',         'entry-gate CALL cond.11'),
        ('CallPcrMinimum',             'entry-gate CALL cond.12 (off by default via EnableCallPcrFilter)'),
        ('CallOiChangeMinimum',        'entry-gate CALL cond.13'),
        ('PriceChangeThreshold',       'entry-gate CALL/PUT [retired] cond.'),
        ('EnableTrendFilter',          'enable-flag CALL/PUT cond.1+2'),
        ('EnableRsiFilter',            'enable-flag CALL/PUT cond.5'),
        ('EnableRsiPullbackFilter',    'enable-flag CALL/PUT cond.5 pullback'),
        ('EnableCandleFilter',         'enable-flag CALL/PUT cond.6+7'),
        ('EnableCallVwapFilter',       'enable-flag CALL cond.3 (commented out - no VWAP data)'),
        ('EnableCallVolumeFilter',     'enable-flag CALL cond.8 (commented out - no volume data)'),
        ('EnableCallPrev5mHighFilter', 'enable-flag CALL cond.4'),
        ('EnableDeltaFilter',          'enable-flag CALL/PUT cond.10'),
        ('EnableCallDayFilter',        'enable-flag CALL cond.11'),
        ('EnableCallPcrFilter',        'enable-flag CALL cond.12 (default OFF)'),
        ('EnableCallAtrFilter',        'enable-flag CALL cond.9'),
        ('EnableCallOiFilter',         'enable-flag CALL cond.13'),
        ('EnableCall5mTrendFilter',    'enable-flag CALL [retired] (default OFF)'),
        ('EnableCallPriceChangeFilter','enable-flag CALL [retired] (default OFF)'),

        -- PUT entry chain (PutEntryEvaluator)
        ('PutRsiFloor',                'entry-gate PUT cond.5'),
        ('PutRsiCeiling',              'entry-gate PUT cond.5'),
        ('RsiPutBouncebackFloor',      'entry-gate PUT cond.5 (bounceback path)'),
        ('RsiPutBouncebackCeiling',    'entry-gate PUT cond.5 (bounceback path)'),
        ('PutBodyPctMinimum',          'entry-gate PUT cond.6+7'),
        ('PutMinAtrPercent',           'entry-gate PUT cond.9'),
        ('PutDeltaMinimum',            'entry-gate PUT cond.10'),
        ('PutDeltaMaximum',            'entry-gate PUT cond.10'),
        ('PutDayChangePctMax',         'entry-gate PUT cond.11'),
        ('PutFallPctMin',              'entry-gate PUT cond.11'),
        ('PutPcrMaximum',              'entry-gate PUT cond.12 (off by default via EnablePutPcrFilter)'),
        ('PutOiChangeMinimum',         'entry-gate PUT cond.13'),
        ('EnablePutVwapFilter',        'enable-flag PUT cond.3 (commented out - no VWAP data)'),
        ('EnablePutVolumeFilter',      'enable-flag PUT cond.8 (commented out - no volume data)'),
        ('EnablePutPrev5mLowFilter',   'enable-flag PUT cond.4'),
        ('EnablePutDayFilter',         'enable-flag PUT cond.11'),
        ('EnablePutPcrFilter',         'enable-flag PUT cond.12 (default OFF)'),
        ('EnablePutAtrFilter',         'enable-flag PUT cond.9'),
        ('EnablePutOiFilter',          'enable-flag PUT cond.13'),
        ('EnablePut5mTrendFilter',     'enable-flag PUT [retired] (default OFF)'),
        ('EnablePutPriceChangeFilter', 'enable-flag PUT [retired] (default OFF)')
    ) AS k(SettingKey, Purpose)
),
Agg AS (
    SELECT
        SettingKey,
        COUNT(*)                                            AS RowCount,
        SUM(CASE WHEN IsActive = 1 THEN 1 ELSE 0 END)       AS ActiveRowCount,
        SUM(CASE WHEN IsActive = 0 THEN 1 ELSE 0 END)       AS InactiveRowCount,
        MAX(CASE WHEN Symbol IS NULL THEN 1 ELSE 0 END)     AS HasGlobalRow,
        STRING_AGG(
            CONCAT(ISNULL(Symbol, '(global)'), '=', SettingValue,
                   CASE WHEN IsActive = 1 THEN '[ON]' ELSE '[OFF]' END),
            ', '
        ) WITHIN GROUP (ORDER BY ISNULL(Symbol, ''))         AS Rows
    FROM dbo.StrategySettings
    GROUP BY SettingKey
)
SELECT
    kk.SettingKey,
    kk.Purpose,
    CASE
        WHEN a.RowCount IS NULL THEN 'NOT CONFIGURED — compiled default applies everywhere'
        WHEN a.ActiveRowCount = a.RowCount THEN 'ALL ROWS ACTIVE'
        WHEN a.ActiveRowCount = 0 THEN 'ALL ROWS INACTIVE — check does not apply where configured'
        ELSE 'MIXED — active for some symbols, inactive for others'
    END                                     AS Status,
    ISNULL(a.RowCount, 0)                   AS RowCount,
    ISNULL(a.ActiveRowCount, 0)             AS ActiveRowCount,
    ISNULL(a.InactiveRowCount, 0)           AS InactiveRowCount,
    a.Rows                                  AS ConfiguredRows
FROM KnownKeys kk
LEFT JOIN Agg a ON a.SettingKey = kk.SettingKey
ORDER BY
    CASE WHEN a.RowCount IS NULL THEN 0 ELSE 1 END,   -- unconfigured keys first
    kk.SettingKey;


/* ── 3. Quick counts ──────────────────────────────────────────────────────── */

SELECT
    (SELECT COUNT(DISTINCT SettingKey) FROM dbo.StrategySettings)                       AS DistinctKeysConfigured,
    (SELECT COUNT(*) FROM dbo.StrategySettings)                                          AS TotalRows,
    (SELECT COUNT(*) FROM dbo.StrategySettings WHERE IsActive = 1)                       AS ActiveRows,
    (SELECT COUNT(*) FROM dbo.StrategySettings WHERE IsActive = 0)                       AS InactiveRows,
    (SELECT COUNT(*) FROM dbo.StrategySettings WHERE Symbol IS NULL)                     AS GlobalRows,
    (SELECT COUNT(*) FROM dbo.StrategySettings WHERE Symbol IS NOT NULL)                 AS SymbolSpecificRows;
