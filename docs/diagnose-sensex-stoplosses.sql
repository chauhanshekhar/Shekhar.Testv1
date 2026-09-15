/*
================================================================================
Post-mortem: 5 SENSEX stop-losses in one session
================================================================================
READ-ONLY. SELECT only. READ UNCOMMITTED so it cannot block the live workers.

Answers, in order:
  1. Which rule actually generated them — the new SENSEX rule, or the ordinary
     Directional engine that the lowered ATR floor also unblocked?
  2. What the indicators looked like at entry.
  3. What exit actually fired, and at what distance.
  4. Whether they clustered in time (one move) or were independent.
  5. What the index did afterwards — was the direction wrong, or the stop too tight?
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

DECLARE @Day date = CAST(GETDATE() AS DATE);   -- change to inspect a past session


/* ── 1. THE FIVE TRADES, and which rule produced each ──────────────────────────
   RuleId is written into StrategyExecutionLog.Reason as "RuleId=..." when an
   approved rule fired. A NULL/absent RuleId means it came from the ORDINARY
   Directional engine — which the SENSEX MinAtrPercent=0.025 row also unblocked.
   That distinction decides what to fix.
   ---------------------------------------------------------------------------- */
SELECT  s.Id,
        s.CreatedOn,
        s.SignalType,
        s.Status,
        s.Confidence,
        s.SelectedStrategy,
        s.MarketState,
        s.EntryPrice        AS IndexEntry,
        s.StopLoss          AS IndexSL,
        s.Target1           AS IndexT1,
        s.OptionSymbol,
        s.OptionLtp         AS OptionEntry,
        s.OptionSL,
        s.OptionT1,
        s.OptionT2,
        s.ExitReason,
        s.ExitTime,
        DATEDIFF(SECOND, s.CreatedOn, s.ExitTime)      AS HeldSeconds,
        s.MfePrice,
        s.MaePrice,
        CASE WHEN log.Reason LIKE 'RuleId=%'
             THEN LEFT(log.Reason, CHARINDEX(' ', log.Reason + ' ') - 1)
             ELSE 'NO RULE - ordinary Directional engine' END AS Origin
FROM        dbo.Signals AS s
LEFT JOIN   dbo.StrategyExecutionLogs AS log
       ON   log.Symbol = s.Symbol
      AND   ABS(DATEDIFF(SECOND, log.CreatedOn, s.CreatedOn)) <= 5
WHERE  s.Symbol = 'SENSEX'
  AND  CAST(s.CreatedOn AS DATE) = @Day
ORDER  BY s.CreatedOn;


/* ── 2. Indicator state at each entry ─────────────────────────────────────────
   Check the rule's own conditions held. If RSI5m/RSI1m sat right on 55/45 and
   ATR barely cleared 0.025, these were marginal entries.
   ---------------------------------------------------------------------------- */
SELECT  s.Id, s.CreatedOn, s.SignalType,
        snap.SnapshotTime, snap.TrendDirection, snap.MarketState,
        snap.Rsi        AS Rsi1m,
        snap.Rsi5m,
        snap.Ema20, snap.Ema50, snap.Ema20_5m, snap.Ema50_5m,
        snap.AtrPct, snap.AtrRaw, snap.Pcr, snap.PriceChangePct, snap.Ltp,
        CASE WHEN snap.Ema20 > snap.Ema50     THEN 'UP' ELSE 'DOWN' END AS Ema1mDir,
        CASE WHEN snap.Ema20_5m > snap.Ema50_5m THEN 'UP' ELSE 'DOWN' END AS Ema5mDir
FROM        dbo.Signals AS s
OUTER APPLY (
    SELECT TOP (1) * FROM dbo.IndicatorSnapshots i
    WHERE  i.Symbol = s.Symbol AND i.SnapshotTime <= s.CreatedOn
    ORDER  BY i.SnapshotTime DESC
) AS snap
WHERE  s.Symbol = 'SENSEX' AND CAST(s.CreatedOn AS DATE) = @Day
ORDER  BY s.CreatedOn;


/* ── 3. WHICH exit fired, and how far the option actually had to fall ──────────
   IMPORTANT: the +20/-20 INDEX exit from the spec was NEVER implemented — I
   reported that at the time. These trades exited on the EXISTING ATR-derived
   OPTION stop (OPTION_SL_HIT via the broker SL_M), not on a 20-index-point stop.
   OptionSlDistancePct shows how much room the option really had.
   ---------------------------------------------------------------------------- */
SELECT  s.Id, s.SignalType, s.ExitReason,
        s.OptionLtp                                   AS OptionEntry,
        s.OptionSL,
        CAST((s.OptionLtp - s.OptionSL) AS DECIMAL(10,2))              AS OptionSlPoints,
        CAST(100.0*(s.OptionLtp - s.OptionSL)/NULLIF(s.OptionLtp,0) AS DECIMAL(6,2)) AS OptionSlDistancePct,
        s.EntryPrice                                  AS IndexEntry,
        s.StopLoss                                    AS IndexSL,
        CAST(ABS(s.EntryPrice - s.StopLoss) AS DECIMAL(10,2))          AS IndexSlPoints,
        s.AtrRawAtEntry, s.AtrPctAtEntry, s.DeltaAtEntry,
        s.MfePrice, s.MaePrice,
        DATEDIFF(SECOND, s.CreatedOn, s.ExitTime)     AS HeldSeconds
FROM   dbo.Signals AS s
WHERE  s.Symbol = 'SENSEX' AND CAST(s.CreatedOn AS DATE) = @Day
ORDER  BY s.CreatedOn;


/* ── 4. Did they cluster? ──────────────────────────────────────────────────────
   Five independent losing setups is a bad rule. Five entries inside a few
   minutes of one another is ONE bad move re-entered repeatedly — a cooldown
   problem, not a signal problem. DuplicateSignalGate's cooldown is 5 minutes.
   ---------------------------------------------------------------------------- */
SELECT  s.Id, s.CreatedOn, s.SignalType, s.ExitTime, s.ExitReason,
        DATEDIFF(SECOND, LAG(s.ExitTime) OVER (ORDER BY s.CreatedOn), s.CreatedOn)
            AS SecondsSincePreviousExit
FROM   dbo.Signals AS s
WHERE  s.Symbol = 'SENSEX' AND CAST(s.CreatedOn AS DATE) = @Day
ORDER  BY s.CreatedOn;


/* ── 5. Was the DIRECTION wrong, or just the stop too tight? ───────────────────
   Index move over the 15 minutes after each entry, in the trade's own direction.
   Positive = the call was right and the stop was too tight.
   Negative = the direction itself was wrong.
   ---------------------------------------------------------------------------- */
SELECT  s.Id, s.CreatedOn, s.SignalType,
        s.EntryPrice AS IndexEntry,
        CAST(MAX(CASE WHEN s.SignalType = 'CALL' THEN f.Ltp - s.EntryPrice
                      ELSE s.EntryPrice - f.Ltp END) AS DECIMAL(10,2)) AS BestMovePts,
        CAST(MIN(CASE WHEN s.SignalType = 'CALL' THEN f.Ltp - s.EntryPrice
                      ELSE s.EntryPrice - f.Ltp END) AS DECIMAL(10,2)) AS WorstMovePts,
        CAST(MAX(CASE WHEN s.SignalType = 'CALL' THEN f.Ltp - s.EntryPrice
                      ELSE s.EntryPrice - f.Ltp END) AS DECIMAL(10,2))
            - 20 AS PointsBeyond20Target
FROM        dbo.Signals AS s
INNER JOIN  dbo.IndicatorSnapshots AS f
       ON   f.Symbol = s.Symbol
      AND   f.SnapshotTime >  s.CreatedOn
      AND   f.SnapshotTime <= DATEADD(MINUTE, 15, s.CreatedOn)
      AND   f.Ltp > 0
WHERE  s.Symbol = 'SENSEX' AND CAST(s.CreatedOn AS DATE) = @Day
GROUP  BY s.Id, s.CreatedOn, s.SignalType, s.EntryPrice
ORDER  BY s.CreatedOn;


/*
================================================================================
STOP THE BLEEDING FIRST — one statement, instant, no redeploy
================================================================================
    UPDATE dbo.StrategySettings SET IsActive = 0, UpdatedOn = SYSDATETIME()
    WHERE  SettingKey = 'EnableSensexDirectionalRule' AND Symbol = 'SENSEX';

If query 1 shows the losses came from the ORDINARY Directional engine rather
than the rule, disable the ATR floor change instead — that is what unblocked it:

    UPDATE dbo.StrategySettings SET IsActive = 0, UpdatedOn = SYSDATETIME()
    WHERE  SettingKey = 'MinAtrPercent' AND Symbol = 'SENSEX';

Both are reversible. Neither needs a deploy. Do this before tuning anything.
================================================================================
*/
