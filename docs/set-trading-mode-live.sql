-- =============================================================================
-- Switch TradingMode Paper -> Live (REAL MONEY)
--
-- GrowwAuthService.GetMode() reads SystemSettings.TradingMode on every call, so this takes
-- effect on the next order without a redeploy. Run outside market hours with no open
-- paper positions.
--
-- WARNING (user's choice, 2026-11-03): MaxDailyTradesGate, CircuitBreakerGate and
-- ConsecutiveLossGate are still commented out in Trading.Worker/SignalWorker.cs.
-- Live will run with NO daily trade cap, NO daily-loss breaker, NO loss-streak stop.
--
-- Only the TradingMode key is selected; SystemSettings holds credentials.
-- =============================================================================

-- PRE-CHECK: current mode and auto-trading state
SELECT [Key], [Value], UpdatedOn
FROM SystemSettings
WHERE [Key] IN ('TradingMode', 'AutoTradingEnabled');

-- PRE-CHECK: no signals still open from paper (should return 0 rows)
SELECT Id, Symbol, Status
FROM Signals
WHERE Status IN ('PENDING', 'ORDER_PLACED', 'ORDER_UNKNOWN', 'ACTIVE',
                 'EXIT_PENDING', 'PARTIAL_EXIT_PENDING', 'CLOSE_FAILED', 'MANUAL_CHECK_REQUIRED');

BEGIN TRANSACTION;

IF EXISTS (SELECT 1 FROM SystemSettings WHERE [Key] = 'TradingMode')
    UPDATE SystemSettings
    SET [Value] = 'Live', UpdatedOn = SYSDATETIME()
    WHERE [Key] = 'TradingMode';
ELSE
    INSERT INTO SystemSettings ([Key], [Value], UpdatedOn)
    VALUES ('TradingMode', 'Live', SYSDATETIME());

COMMIT TRANSACTION;

-- VERIFY: expect Value = 'Live'
SELECT [Key], [Value], UpdatedOn
FROM SystemSettings
WHERE [Key] = 'TradingMode';

-- =============================================================================
-- ROLLBACK (back to paper)
-- =============================================================================
-- UPDATE SystemSettings
-- SET [Value] = 'Paper', UpdatedOn = SYSDATETIME()
-- WHERE [Key] = 'TradingMode';
--
-- SELECT [Key], [Value], UpdatedOn FROM SystemSettings WHERE [Key] = 'TradingMode';
