/*
================================================================================
 NoRsiCall — PAPER trading, own position slot
================================================================================
CALL entry = the live CALL chain WITHOUT the RSI band (EMA20 > EMA50, LTP > EMA20,
EMA gap, PCR band, ATR, EarlyTrendGate).
  * PAPER ONLY - enforced in code; in Live mode it never trades.
  * Last fallback: only when the existing strategy, the approved rules AND Rated
    CALL produced no signal that cycle, so it never pre-empts them.
  * Signals are tagged SelectedStrategy = 'NoRsiCall'.
  * Every post-gate, SL/T1/T2, exit flow, TradeJournal and duplicate protection
    apply as for any other signal.

NoRsiOwnPositionSlot = 1 gives it its own slot: one NoRsiCall position beside
the existing strategy's (and Rated CALL's), never on the same symbol + direction
as any open position.

Evidence: docs/research/2026-10-29-backtest-condition-live-interchange - CALL
without the RSI band beat live in total (+7,666 vs -4,315 after costs) but the
gain was in the training period only. This paper run is the test.

Needs the Worker build that contains NoRsiCallShadowEvaluator.BlockTrade.
Picked up within the settings cache's 5-minute refresh.
================================================================================
*/

/* ---------------------------------------------------------------------------
   ENABLE (global rows)
   --------------------------------------------------------------------------- */
IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'EnableNoRsiCallTrading' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'EnableNoRsiCallTrading' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('EnableNoRsiCallTrading', NULL, 1, 1, 'Paper-only CALL chain without the RSI band.', SYSDATETIME());

IF EXISTS (SELECT 1 FROM dbo.StrategySettings WHERE SettingKey = 'NoRsiOwnPositionSlot' AND Symbol IS NULL)
    UPDATE dbo.StrategySettings SET SettingValue = 1, IsActive = 1, UpdatedOn = SYSDATETIME()
     WHERE SettingKey = 'NoRsiOwnPositionSlot' AND Symbol IS NULL;
ELSE
    INSERT INTO dbo.StrategySettings (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES ('NoRsiOwnPositionSlot', NULL, 1, 1, 'NoRsiCall gets its own position slot beside the existing strategy.', SYSDATETIME());

/* ---------------------------------------------------------------------------
   ROLLBACK - turn trading off FIRST; wait for any open NoRsiCall position to
   close before turning the slot off.
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'EnableNoRsiCallTrading' AND Symbol IS NULL;
UPDATE dbo.StrategySettings SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = 'NoRsiOwnPositionSlot' AND Symbol IS NULL;
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   VERIFY
   --------------------------------------------------------------------------- */
SELECT SettingKey, Symbol, SettingValue, IsActive, UpdatedOn
FROM   dbo.StrategySettings
WHERE  SettingKey IN ('EnableNoRsiCallTrading', 'NoRsiOwnPositionSlot', 'EnableRatedCallTrading', 'RatedOwnPositionSlot')
ORDER BY SettingKey;

/* CONFIRM: NoRsiCall and RatedCall paper trades side by side */
SELECT s.SelectedStrategy, s.Id, s.CreatedOn, s.Symbol, s.Status, s.OptionLtp, j.ProfitLoss
FROM   dbo.Signals s
LEFT JOIN dbo.TradeJournal j ON j.SignalId = s.Id
WHERE  s.SelectedStrategy IN ('NoRsiCall', 'RatedCall')
ORDER BY s.CreatedOn DESC;
