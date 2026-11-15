/* ============================================================================
   CrossIndexAgreementGate - enable / tune / kill switch                2026-10-26
   ----------------------------------------------------------------------------
   The gate ships OFF. Nothing happens until EnableCrossIndexAgreementGate = 1.
   Claude does not execute this file (.claude/rules/02-approval-gate.md).

   WHAT IT DOES
   ------------
   Blocks a signal unless the OTHER indices moved in its direction over the last
   CrossIndexLookbackMinutes closed 1-minute bars: a CALL needs them up, a PUT
   needs them down. A BANKNIFTY PUT needs NIFTY and SENSEX both lower than N
   minutes ago. Code: src/Trading.Worker/SignalGates/CrossIndexAgreementGate.cs

   EVIDENCE (docs/research/2026-10-26-cross-index-agreement/)
   ----------------------------------------------------------
   263 closed signals 2026-08-04..10-26, index-candle replay, closed bars only:
     both others agree : 174 signals, stop rate 35%, avg +0.27R
     otherwise         :  89 signals, stop rate 44%, avg -0.12R
   After costs (Rs20/order + 1pt/side, 4 lots): agree +55k PF 1.34, rest -36k PF 0.69.
   Same ordering in both halves, all 3 symbols, CALL and PUT.

   *** READ BEFORE ENABLING ***
   - Loss AVOIDER, not a proven edge. The agreeing group was only break-even after costs
     in the first half; ~15 feature cells were looked at (p 0.003 raw, ~0.05 corrected);
     28 days of data. At 2pt slippage the agreeing group is only +16k.
   - Enable on PAPER first. Re-run the research scripts after ~10 sessions on signals
     created after 2026-10-26 (docs/research/2026-10-26-cross-index-agreement/README.md)
     and keep it only if the agreeing group is still clearly better.
   - Blocks about a third of signals. If an index feed has no candles it is not counted
     (fail open); if none of the others has data the signal passes.

   SAFETY
   ------
   - Idempotent upsert; re-running changes nothing new.
   - Touches only EnableCrossIndexAgreementGate / CrossIndex* rows.
   - Kill switch (ROLLBACK) and VERIFY at the bottom.
   - Settings cache refreshes ~5 minutes; no restart needed once the Worker build with the gate is deployed.
   ============================================================================ */

SET NOCOUNT ON;
GO

DECLARE @Symbol   nvarchar(50)  = NULL;   -- NULL = global, or 'BANKNIFTY' to start with one index
DECLARE @Enable   decimal(18,6) = 0;      -- <<< 1 to turn the gate ON
DECLARE @Lookback decimal(18,6) = 15;     -- minutes of closed 1m bars compared (research used 15)
DECLARE @MinAgree decimal(18,6) = 2;      -- others that must agree; 2 = both
DECLARE @Side     decimal(18,6) = 0;      -- 0 CALL+PUT, 1 CALL only, 2 PUT only

DECLARE @rows TABLE (SettingKey nvarchar(100), SettingValue decimal(18,6), Descr nvarchar(400));
INSERT INTO @rows VALUES
 (N'EnableCrossIndexAgreementGate', @Enable,   N'Cross-index agreement gate on/off. See docs/set-cross-index-agreement-gate.sql'),
 (N'CrossIndexLookbackMinutes',     @Lookback, N'Minutes of closed 1m bars compared on the other indices'),
 (N'CrossIndexMinAgree',            @MinAgree, N'How many other indices must move in the trade direction (2 = both)'),
 (N'CrossIndexAgreementSide',       @Side,     N'0 CALL and PUT, 1 CALL only, 2 PUT only');

MERGE dbo.StrategySettings AS t
USING @rows AS s
   ON t.SettingKey = s.SettingKey
  AND ISNULL(t.Symbol, N'~') = ISNULL(@Symbol, N'~')
WHEN MATCHED THEN
    UPDATE SET SettingValue = s.SettingValue, IsActive = 1, UpdatedOn = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (s.SettingKey, @Symbol, s.SettingValue, 1, s.Descr, SYSDATETIME());

PRINT CONCAT('CrossIndexAgreementGate rows upserted: ', @@ROWCOUNT);
GO

/* ---------------------------------------------------------------------------
   VERIFY
   --------------------------------------------------------------------------- */
SELECT SettingKey, ISNULL(Symbol, N'(global)') AS Scope, SettingValue, IsActive, UpdatedOn
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey = N'EnableCrossIndexAgreementGate' OR SettingKey LIKE N'CrossIndex%'
ORDER BY SettingKey, Symbol;
GO

/* ---------------------------------------------------------------------------
   ROLLBACK / KILL SWITCH - turns the gate off immediately (no restart).
   Run this block ALONE to disable.
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = N'EnableCrossIndexAgreementGate';
   --------------------------------------------------------------------------- */
