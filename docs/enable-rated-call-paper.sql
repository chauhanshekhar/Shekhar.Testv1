/*
================================================================================
 RATED CALL (no RSI) — PAPER trading, own position slot
================================================================================
Scores CALL vs PUT each minute (trend 30, EMA gap 15, momentum 20, PCR 20,
ATR 15 - no RSI) and BUYS a CALL when CALL scores >= 65 and beats PUT by >= 30.
  * PAPER ONLY - enforced in code; in Live mode it never trades.
  * Runs only when the existing strategy and approved rules produced NO signal
    that cycle, so it never pre-empts them.
  * Signals are tagged SelectedStrategy = 'RatedCall'.
  * Every post-gate, SL/T1/T2, exit flow, TradeJournal and duplicate protection
    apply exactly as for any other signal.

RatedOwnPositionSlot = 1 gives Rated CALL its own slot: it may hold one position
beside the existing strategy's position, never on the same symbol + direction.
Gemini is unaffected (it keeps sharing the chain slot unless
GeminiIgnoreOnePosition = 1).

Evidence: docs/research/2026-10-29-rated-direction (live-exit replay,
10-07..10-28, after costs): +47,673, 67 trades, PF 1.57, positive in train,
validation and holdout. Thresholds were fitted on these 15 sessions.

Needs the Worker build that contains RatedDirectionScorer.DecideCallTrade.
Picked up within the settings cache's 5-minute refresh.
================================================================================
*/

/* ---------------------------------------------------------------------------
   ENABLE (global rows)
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EnableRatedCallTrading' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableRatedCallTrading' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableRatedCallTrading', NULL, 1, 1, 'Paper-only Rated CALL (no RSI): trade CALL when it wins the CALL vs PUT score.', SYSDATETIME());

IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'RatedOwnPositionSlot' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'RatedOwnPositionSlot' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('RatedOwnPositionSlot', NULL, 1, 1, 'Rated CALL gets its own position slot beside the existing strategy.', SYSDATETIME());

IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'RatedMinScore' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings SET SettingValue = 65, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'RatedMinScore' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('RatedMinScore', NULL, 65, 1, 'Rated direction: minimum winning score (0-100).', SYSDATETIME());

IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'RatedMinMargin' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings SET SettingValue = 30, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'RatedMinMargin' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('RatedMinMargin', NULL, 30, 1, 'Rated direction: winner must beat the other side by this many points.', SYSDATETIME());

/* ---------------------------------------------------------------------------
   ROLLBACK (stop Rated CALL trading and return to one position at a time)
   Turn trading off FIRST; wait for any open RatedCall position to close
   before turning the slot off.
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'EnableRatedCallTrading' AND Symbol IS NULL;
UPDATE dbo.StrategySettings SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'RatedOwnPositionSlot' AND Symbol IS NULL;
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   VERIFY
   --------------------------------------------------------------------------- */
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey IN ('EnableRatedCallTrading', 'RatedOwnPositionSlot', 'RatedMinScore', 'RatedMinMargin',
                      'GeminiIgnoreOnePosition')
ORDER BY SettingKey;

/* CONFIRM: Rated CALL paper trades and their result */
SELECT s.Id, s.CreatedOn, s.Symbol, s.SignalType, s.Status, s.Confidence, s.OptionLtp,
       j.ProfitLoss
FROM   dbo.Signals s
LEFT JOIN dbo.TradeJournal j ON j.SignalId = s.Id
WHERE  s.SelectedStrategy = 'RatedCall'
ORDER BY s.CreatedOn DESC;
