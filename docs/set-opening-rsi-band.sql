/* ============================================================================
   Disable SessionTrendGate, enable the opening-conditional RSI band  2026-10-16
   ----------------------------------------------------------------------------
   Claude does not execute this (.claude/rules/02-approval-gate.md). Run it
   yourself; settings cache refreshes in ~5 minutes, no restart needed.

   WHAT CHANGES
   ------------
   1. SessionTrendGate OFF. It required the session to have already run 0.75 ATR
      in the trade's direction AND still be within 1 ATR of the session extreme,
      which would mask the band change below.

   2. Opening-conditional RSI band ON. The live band is one pair for every
      session; a gap-up session runs at a higher RSI than a flat one, so the
      CALL ceiling of 65 refuses exactly the gap-up entries that work.

        normal open   CALL 45-65   PUT 28-38     (unchanged)
        gap up        CALL 45-85   PUT 45-60
        gap down      (not set - normal band applies)

      Read where the band is read, in SignalEngineService condition 5, so it can
      WIDEN as well as narrow. A post-signal gate cannot do this: a gate only
      blocks, so it can never let a high-RSI CALL through.

   WHY THESE NUMBERS
   -----------------
   From the pure snapshot base rate (docs/research/2026-10-16-snapshot-forward/,
   20,493 minutes, no gates, forward 30-minute index move in ATRs):

     RSI >= 70, all sessions ......... +0.53 ATR, 59% up  (best up-drift)
     GapUp x RSI >= 75 ............... +0.84 ATR, 63% up  (n=288)
     GapUp PUT, RSI 45-60 ............ +193/trade          (live 28-38: -254)

   NOT walk-forward validated as a package. The gap-up PUT band failed
   validation when tested on its own (docs/research/2026-10-16-gapup-band/), and
   CALL could not be tested at all (5 training trades). This is a paper-mode
   experiment to generate the trades that would let it be tested - not a result.

   SAFETY
   ------
   - Idempotent. Re-running changes nothing.
   - An override needs BOTH its floor and ceiling rows active, so a half
     configured pair can never widen one end of a band.
   - Normal-open sessions keep the live band exactly.
   - Kill switch and full rollback at the bottom.
   ============================================================================ */

SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------------------
   BEFORE
   --------------------------------------------------------------------------- */
SELECT SettingKey, ISNULL(Symbol, N'(global)') AS Scope, SettingValue, IsActive
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey LIKE N'%SessionTrend%'
   OR SettingKey LIKE N'%OpeningRsi%'
   OR SettingKey LIKE N'GapUp%Rsi%'
   OR SettingKey IN (N'CallRsiFloor', N'CallRsiCeiling', N'PutRsiFloor', N'PutRsiCeiling')
ORDER BY SettingKey, Symbol;

/* ---------------------------------------------------------------------------
   1. SessionTrendGate OFF (tuning rows left in place)
   --------------------------------------------------------------------------- */
UPDATE dbo.StrategySettings
   SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = N'EnableSessionTrendGate';

PRINT CONCAT('SessionTrendGate disabled, rows updated = ', @@ROWCOUNT);

/* ---------------------------------------------------------------------------
   2. Opening-conditional RSI band ON
   --------------------------------------------------------------------------- */
MERGE dbo.StrategySettings AS t
USING (VALUES
 (N'EnableOpeningRsiBand',  1.00, N'Opening-conditional RSI band on/off (SignalEngineService condition 5)'),
 (N'OpeningRsiGapPct',      0.30, N'Gap percent separating gap-up / normal / gap-down'),
 (N'GapUpCallRsiFloor',    45.00, N'CALL 5m RSI floor on gap-up sessions'),
 (N'GapUpCallRsiCeiling',  85.00, N'CALL 5m RSI ceiling on gap-up sessions (live band stops at 65)'),
 (N'GapUpPutRsiFloor',     45.00, N'PUT 5m RSI floor on gap-up sessions'),
 (N'GapUpPutRsiCeiling',   60.00, N'PUT 5m RSI ceiling on gap-up sessions (live band is 28-38)')
) AS s(SettingKey, SettingValue, Descr)
   ON t.SettingKey = s.SettingKey AND t.Symbol IS NULL
WHEN MATCHED THEN
    UPDATE SET SettingValue = s.SettingValue, IsActive = 1, UpdatedOn = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (s.SettingKey, NULL, s.SettingValue, 1, s.Descr, SYSDATETIME());

PRINT CONCAT('Opening RSI band rows upserted = ', @@ROWCOUNT);
GO

/* ---------------------------------------------------------------------------
   VERIFY
   Expect: EnableSessionTrendGate 0, EnableOpeningRsiBand 1,
           GapUpCall 45/85, GapUpPut 45/60, and the live bands untouched
           (CallRsi 45/65, PutRsi 28/38).
   --------------------------------------------------------------------------- */
SELECT SettingKey, ISNULL(Symbol, N'(global)') AS Scope, SettingValue, IsActive, UpdatedOn
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey LIKE N'%SessionTrend%'
   OR SettingKey LIKE N'%OpeningRsi%'
   OR SettingKey LIKE N'GapUp%Rsi%'
   OR SettingKey IN (N'CallRsiFloor', N'CallRsiCeiling', N'PutRsiFloor', N'PutRsiCeiling')
ORDER BY SettingKey, Symbol;

/* ---------------------------------------------------------------------------
   KILL SWITCH — back to today's behaviour, keeping the rows.
   ---------------------------------------------------------------------------
UPDATE dbo.StrategySettings SET SettingValue = 0, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = N'EnableOpeningRsiBand';
   --------------------------------------------------------------------------- */

/* ---------------------------------------------------------------------------
   FULL ROLLBACK — remove the override rows entirely.
   ---------------------------------------------------------------------------
DELETE FROM dbo.StrategySettings
 WHERE SettingKey LIKE N'%OpeningRsi%' OR SettingKey LIKE N'GapUp%Rsi%';
   --------------------------------------------------------------------------- */
