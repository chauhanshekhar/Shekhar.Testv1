/*
================================================================================
Force the 1-minute entry trigger candle in BOTH paper and live
================================================================================
WHY
StrategyManager.cs:165 picks the entry trigger candle from the trading MODE:

    var defaultToOneMin = ctx.Orders.CurrentMode == TradingMode.Live;
    var useOneMinTrigger = ctx.StrategySettings.GetBool("UseOneMinuteEntryCandle", symbol, defaultToOneMin);
    var triggerCandle = useOneMinTrigger ? market.LastClosed1mCandle : market.LastClosed5mCandle;

With no StrategySettings row present, that means:
    Live  -> 1-minute closed candle
    Paper -> 5-minute closed candle

So paper and live are confirming entries on different timeframes, and paper
results cannot be read as a prediction of live behaviour. This row removes the
mode dependency and pins BOTH to the 1-minute closed candle.

WHAT THIS DOES AND DOES NOT CHANGE
Changes: which closed candle supplies body% and direction confirmation for the
         Directional entry check (SignalEngineService candleBullish/candleBearish).
Does NOT change: RSI (already 1m), EMA20/EMA50 (1m), ATR (1m), the 5m EMA trend
         filter, SL/T1/T2, option selection, quantity, or any gate.

EXPECT MORE TRADES. A 1-minute candle's body% clears BodyPctMinimum far more
often than a 5-minute one, so paper trade count will rise once this is applied.
That is the point — it is what live has been doing all along.

Symbol = NULL is deliberate: this is a global setting so every instrument uses
the same trigger. Per-symbol rows override it if ever needed.

TAKES EFFECT: StrategySettingsService caches in memory and refreshes every
5 minutes — no restart needed, allow up to 5 minutes.

Idempotent. Touches exactly one row. READ the BEFORE result before committing.
================================================================================
*/

SET XACT_ABORT ON;
SET NOCOUNT ON;

-- ── 1. BEFORE ────────────────────────────────────────────────────────────────
SELECT 'BEFORE' AS Stage, Id, SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey = 'UseOneMinuteEntryCandle';


-- ── 2. APPLY ─────────────────────────────────────────────────────────────────
BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM dbo.StrategySettings
               WHERE SettingKey = 'UseOneMinuteEntryCandle' AND Symbol IS NULL)
    BEGIN
        UPDATE dbo.StrategySettings
        SET    SettingValue = 1,           -- GetBool treats any non-zero as true
               IsActive     = 1,
               Description  = 'Entry trigger = 1-minute closed candle in both paper and live (removes the mode-dependent default)',
               UpdatedOn    = SYSDATETIME()
        WHERE  SettingKey = 'UseOneMinuteEntryCandle'
          AND  Symbol IS NULL;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
        VALUES ('UseOneMinuteEntryCandle', NULL, 1, 1,
                'Entry trigger = 1-minute closed candle in both paper and live (removes the mode-dependent default)',
                SYSDATETIME());
    END

COMMIT TRANSACTION;


-- ── 3. VERIFY ────────────────────────────────────────────────────────────────
-- Expect one row: Symbol NULL, SettingValue 1, IsActive 1.
-- Any per-symbol row for this key would override the global one — check for those too.
SELECT 'AFTER' AS Stage, Id, SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey = 'UseOneMinuteEntryCandle';


/*
================================================================================
ROLLBACK — returns to the mode-dependent default (Live 1m / Paper 5m)
================================================================================
    UPDATE dbo.StrategySettings
    SET    IsActive  = 0,
           UpdatedOn = SYSDATETIME()
    WHERE  SettingKey = 'UseOneMinuteEntryCandle'
      AND  Symbol IS NULL;

Inactive rows are ignored by the resolver, so the caller default applies again.
Allow up to 5 minutes for the cache to refresh.
================================================================================
*/
