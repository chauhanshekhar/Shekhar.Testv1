-- Request a manual close of the open BANKNIFTY PAPER signal.
--
-- Writes ONE SystemSettings row ("ManualClose:{SignalId}"). The ExitWorker reads it on its next
-- 1-second cycle and closes the signal through its normal paper exit path (reason MANUAL_CLOSE).
-- REQUIRES the Worker build that contains the ManualClose change to be deployed; on an older
-- Worker the row is ignored and nothing happens.
--
-- Paper mode only. Do not use for live positions - square those off at the broker.
-- Run STEP 1, confirm exactly one signal, then run STEP 2.

-- ── STEP 1: find the open BANKNIFTY signal (read-only) ───────────────────────────────────────
SELECT Id, Symbol, SignalType, OptionSymbol, Status, CreatedOn
FROM   dbo.Signals
WHERE  Symbol = 'BANKNIFTY'
  AND  Status = 'ACTIVE'
ORDER  BY CreatedOn DESC;

-- ── STEP 2: request the close (idempotent) ───────────────────────────────────────────────────
DECLARE @SignalId bigint = NULL;   -- <-- set to the Id from STEP 1

IF @SignalId IS NULL
    THROW 50000, 'Set @SignalId from STEP 1 before running.', 1;

IF NOT EXISTS (SELECT 1 FROM dbo.Signals WHERE Id = @SignalId AND Symbol = 'BANKNIFTY' AND Status = 'ACTIVE')
    THROW 50001, 'Signal is not an ACTIVE BANKNIFTY signal. Nothing written.', 1;

DECLARE @Key nvarchar(200) = CONCAT('ManualClose:', @SignalId);
DECLARE @NowIst datetime2 = DATEADD(MINUTE, 330, SYSUTCDATETIME());   -- IstClock.Now

IF EXISTS (SELECT 1 FROM dbo.SystemSettings WHERE [Key] = @Key)
    UPDATE dbo.SystemSettings SET [Value] = CONVERT(nvarchar(50), @NowIst, 126), UpdatedOn = @NowIst WHERE [Key] = @Key;
ELSE
    INSERT INTO dbo.SystemSettings ([Key], [Value], UpdatedOn)
    VALUES (@Key, CONVERT(nvarchar(50), @NowIst, 126), @NowIst);

-- ── VERIFY (re-run after ~10 s; the request row disappears once the signal is closed) ────────
-- SELECT [Key], UpdatedOn FROM dbo.SystemSettings WHERE [Key] LIKE 'ManualClose:%';      -- pending request
-- SELECT Id, Status, ExitReason FROM dbo.Signals WHERE Id = <SignalId>;                  -- expect CLOSED / MANUAL_CLOSE

-- ── ROLLBACK (cancel the request before the Worker has acted on it) ──────────────────────────
-- DELETE FROM dbo.SystemSettings WHERE [Key] = 'ManualClose:<SignalId>';
