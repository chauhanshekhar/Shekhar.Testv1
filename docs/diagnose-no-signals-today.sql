/*
================================================================================
 Why did no signal generate today?
================================================================================
READ-ONLY.

Confidence = 0 is NOT itself the problem — StrategyManager.cs:150 hardcodes 0m
whenever there is no candidate signal to score at all, which is the normal
outcome on almost every cycle (only 6 of 45,665 historical snapshots ever
cleared the old 70-confidence floor). It is a symptom of "no signal", not a
separate bug. BlockedBy/BlockedReason are the actual explanation.

PART 1 — is the pipeline even running today (feed alive, snapshots being
         written, indicators non-zero)?
PART 2 — where is everything being blocked, and how many rows show it?
PART 3 — sample rows for the top blocker, so the actual numbers are visible.
================================================================================
*/

SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

DECLARE @Today date = CAST(GETDATE() AS date);


/* ============================================================================
   PART 1 — is data even flowing today
   ============================================================================
   LastSnapshot should be within the last minute or two during market hours.
   If it is stale (or Rows is 0), nothing downstream matters — the worker
   itself, the market feed, or the broker connection is the problem, not the
   signal logic.
   ============================================================================ */
SELECT
    Symbol,
    COUNT(*)                                   AS Rows,
    MIN(SnapshotTime)                          AS FirstSnapshot,
    MAX(SnapshotTime)                          AS LastSnapshot,
    DATEDIFF(SECOND, MAX(SnapshotTime), GETDATE()) AS SecondsSinceLastSnapshot,
    SUM(CASE WHEN Ltp = 0 THEN 1 ELSE 0 END)   AS ZeroLtpRows,
    SUM(CASE WHEN Ema20 = 0 THEN 1 ELSE 0 END) AS ZeroEma20Rows,
    SUM(CASE WHEN Rsi = 0 THEN 1 ELSE 0 END)   AS ZeroRsiRows
FROM dbo.IndicatorSnapshots
WHERE CAST(SnapshotTime AS date) = @Today
GROUP BY Symbol
ORDER BY Symbol;


/* ============================================================================
   PART 2 — blocker breakdown for today
   ============================================================================
   BlockedBy = 'IndicatorsNotReady' at high volume usually means EMA warm-up
   never finished (candle gap, feed restarted mid-session).
   BlockedBy = 'StrategySelector' means MarketClassificationService itself
   never chose Directional/HeroZero — the regime/mode logic, not a threshold.
   BlockedBy = 'SignalEngine' means a strategy WAS selected but its own
   AND-chain (and the rule fallback) rejected every cycle — this is where
   RSI/ATR/PriceChangePct/MarketState thresholds live.
   A gate name (e.g. 'AtrGate', 'OnePositionGate') means a pre/post gate.
   ============================================================================ */
SELECT
    Symbol,
    ISNULL(BlockedBy, '(none / signal generated)') AS BlockedBy,
    COUNT(*)                                        AS Rows,
    CAST(COUNT(*) * 100.0 / SUM(COUNT(*)) OVER (PARTITION BY Symbol)
         AS DECIMAL(5,2))                            AS PctOfSymbol,
    SUM(CASE WHEN SignalGenerated = 1 THEN 1 ELSE 0 END) AS SignalsGenerated
FROM dbo.IndicatorSnapshots
WHERE CAST(SnapshotTime AS date) = @Today
GROUP BY Symbol, BlockedBy
ORDER BY Symbol, Rows DESC;


/* ============================================================================
   PART 3 — sample rows + the actual indicator values, for the most common
   blocker per symbol. This is what turns "SignalEngine blocked it" into
   "RSI was 48, needed <=35" — the number that says whether it's a quiet
   market or a broken calculation.
   ============================================================================ */
;WITH TopBlocker AS (
    SELECT Symbol, BlockedBy,
           ROW_NUMBER() OVER (PARTITION BY Symbol ORDER BY COUNT(*) DESC) AS rn
    FROM dbo.IndicatorSnapshots
    WHERE CAST(SnapshotTime AS date) = @Today AND BlockedBy IS NOT NULL
    GROUP BY Symbol, BlockedBy
)
SELECT TOP (30)
    s.Symbol, s.SnapshotTime, s.BlockedBy, s.BlockedReason,
    s.Ltp, s.Ema20, s.Ema50, s.Ema20_5m, s.Ema50_5m,
    s.Rsi, s.Rsi5m, s.AtrPct, s.PriceChangePct,
    s.TrendDirection, s.MarketState, s.StrategySelected, s.Confidence
FROM dbo.IndicatorSnapshots s
JOIN TopBlocker t ON t.Symbol = s.Symbol AND t.BlockedBy = s.BlockedBy AND t.rn = 1
WHERE CAST(s.SnapshotTime AS date) = @Today
ORDER BY s.Symbol, s.SnapshotTime DESC;
