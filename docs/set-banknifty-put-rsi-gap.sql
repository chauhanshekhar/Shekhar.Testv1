/* ============================================================================
   BANKNIFTY — widen the PUT RSI band to 25-40 and lower the EMA gap to 10
   ----------------------------------------------------------------------------
                                                                     2026-10-16
   Claude does not execute this (.claude/rules/02-approval-gate.md).
   Evidence: docs/research/2026-10-16-backtest-condition-both/

   WHAT CHANGES (BANKNIFTY only; other symbols untouched)
   ------------------------------------------------------
     PutRsiFloor      28 -> 25     (new BANKNIFTY rows; global 28/38 stays
     PutRsiCeiling    38 -> 40      in force for NIFTY and SENSEX)
     EmaGapMinPoints  27.5 -> 10   (existing BANKNIFTY row)

   MEASURED, costed at Rs20/order + 1pt slippage, walk-forward:

     BANKNIFTY PUT          n    net       PF   val n/PF   test PF
       baseline            104   +20,921   1.68   26/1.03    4.42
       RSI 25-40, gap 10   118   +30,644   1.77   30/1.39    4.84
       RSI 25-40, gap 20    50    +5,430   1.49    9/5.73    2.46
       RSI 25-40, gap 27.5  (not in the grid; gap 20 is the nearest proxy)

   The RSI widening only pays at a LOW gap - at the current 27.5 it is worse
   than baseline. The two interact; do not apply one without the other.

   *** WHY GAP 10 AND NOT GAP 5 ***
   EmaGapMinPoints is a SINGLE key read by both sides (EmaGapCondition.cs) -
   there is no PutEmaGapMinPoints - so a BANKNIFTY change hits CALL too:

     BANKNIFTY CALL        n    net       PF
       baseline (27.5)      13   +37,841   5.79
       gap 10               13   +37,841   5.79   <- identical, no CALL impact
       gap 5                28   +24,809   2.22   <- costs ~13,000

   Gap 5 looked best for PUT in isolation (+39,531) but the CALL side effect
   takes roughly 13,000 of it back. Gap 10 takes no CALL trade at all and has
   the better validation profit factor. That is why this script uses 10.

   *** SIDE EFFECT: LOT SIZING ***
   PutRsiFloor / PutRsiCeiling also drive the RSI term of the confidence score,
   which feeds position size (SignalEngineService.RsiConfidenceBand):
       rsi <= PutRsiFloor -> +15, else <= PutRsiCeiling -> +8
   After this, RSI 25-28 scores +15 instead of +8, and 38-40 scores +8 instead
   of 0. Confidence rises, so size rises. The backtest uses a FIXED lot count
   (AdaptiveLotMin = 4) and reads confidence nowhere, so +30,644 is at constant
   size. Live, profit and drawdown both scale by an amount this does not measure.
   Watch lot sizes on the first sessions.

   OTHER CAVEATS
   -------------
   - Costs here assume 1pt slippage. Measured BANKNIFTY entry slippage is 0.000
     across 16 live fills, so the real figure is better than stated. Exit
     slippage is not measured.
   - 30 validation trades. Validated, not large.
   - The gap-up RSI override (GapUpPutRsiFloor/Ceiling 40/60) still applies on
     gap-up sessions and takes precedence over this band there.

   SAFETY: idempotent; BANKNIFTY-scoped; ROLLBACK and VERIFY below. Settings
   cache refreshes ~5 minutes, no restart.
   ============================================================================ */

SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------------------
   BEFORE — expect global PutRsi 28/38 and a BANKNIFTY EmaGapMinPoints of 27.5
   --------------------------------------------------------------------------- */
SELECT SettingKey, ISNULL(Symbol, N'(global)') AS Scope, SettingValue, IsActive
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey IN (N'PutRsiFloor', N'PutRsiCeiling', N'EmaGapMinPoints')
ORDER BY SettingKey, Symbol;

/* ---------------------------------------------------------------------------
   APPLY
   --------------------------------------------------------------------------- */
MERGE dbo.StrategySettings AS t
USING (VALUES
 (N'PutRsiFloor',     25.00, N'BANKNIFTY PUT 5m RSI floor 25 (global 28). Paired with EmaGapMinPoints 10 - see docs/set-banknifty-put-rsi-gap.sql'),
 (N'PutRsiCeiling',   40.00, N'BANKNIFTY PUT 5m RSI ceiling 40 (global 38). Paired with EmaGapMinPoints 10'),
 (N'EmaGapMinPoints', 10.00, N'BANKNIFTY EMA gap minimum 10 (was 27.5). Applies to CALL and PUT; CALL measured unchanged at 10')
) AS s(SettingKey, SettingValue, Descr)
   ON t.SettingKey = s.SettingKey AND t.Symbol = N'BANKNIFTY'
WHEN MATCHED THEN
    UPDATE SET SettingValue = s.SettingValue, IsActive = 1, UpdatedOn = SYSDATETIME()
WHEN NOT MATCHED THEN
    INSERT (SettingKey, Symbol, SettingValue, IsActive, Description, UpdatedOn)
    VALUES (s.SettingKey, N'BANKNIFTY', s.SettingValue, 1, s.Descr, SYSDATETIME());

PRINT CONCAT('BANKNIFTY rows upserted = ', @@ROWCOUNT);
GO

/* ---------------------------------------------------------------------------
   VERIFY — expect BANKNIFTY PutRsi 25/40 and EmaGapMinPoints 10,
            global PutRsi 28/38 and EmaGapMinPoints 10 unchanged.
   --------------------------------------------------------------------------- */
SELECT SettingKey, ISNULL(Symbol, N'(global)') AS Scope, SettingValue, IsActive, UpdatedOn
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey IN (N'PutRsiFloor', N'PutRsiCeiling', N'EmaGapMinPoints')
ORDER BY SettingKey, Symbol;

/* Nothing outside BANKNIFTY changed today: expect 0 rows. */
SELECT COUNT(*) AS OtherSymbolRowsTouchedToday
FROM dbo.StrategySettings WITH (NOLOCK)
WHERE SettingKey IN (N'PutRsiFloor', N'PutRsiCeiling', N'EmaGapMinPoints')
  AND ISNULL(Symbol, N'~') <> N'BANKNIFTY'
  AND CONVERT(date, UpdatedOn) = CONVERT(date, SYSDATETIME());

/* ---------------------------------------------------------------------------
   ROLLBACK — drop the BANKNIFTY RSI rows (global 28/38 applies again) and put
   the EMA gap back to 27.5. DELETE, not IsActive = 0: an inactive RSI row is
   NOT the same as no row (it also drops the RSI confidence term).
   ---------------------------------------------------------------------------
DELETE FROM dbo.StrategySettings
 WHERE SettingKey IN (N'PutRsiFloor', N'PutRsiCeiling') AND Symbol = N'BANKNIFTY';

UPDATE dbo.StrategySettings
   SET SettingValue = 27.50, UpdatedOn = SYSDATETIME()
 WHERE SettingKey = N'EmaGapMinPoints' AND Symbol = N'BANKNIFTY';
   --------------------------------------------------------------------------- */
