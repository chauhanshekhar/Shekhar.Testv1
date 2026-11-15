# EMA9 above EMA21 (1m) as an extra entry condition, 2026-11-04

Method: `dotnet run --project src/Trading.Backtest -- --fast-ema --side BOTH --cost-per-order 20 --slippage-points 1 --dump-trades --no-grid --min-trades 1`.
Live rules + (CALL: 1m EMA9 > EMA21, PUT: EMA9 < EMA21), split by opening type (gap threshold 0.30%).
EMA9/EMA21 are rebuilt from 1m candle closes at the decision minute (not stored in snapshots; no look-ahead).
Chronological split: train 60 / validation 20 / test 20. Net = after costs. Cells tested: 6 per side (1 filter x 3 opening types x baseline/filter). Selector replay NOT applied. Parity audit (Stage 2) not run.

| Arm | Trades base -> filter | Net after costs base -> filter | PF | Test-slice net (n) |
|---|---|---|---|---|
| CALL GapUp | 40 -> 37 | +19,323 -> +26,051 | 2.14 -> 2.74 | +21,738 -> +21,738 (9, identical) |
| CALL GapDown | 65 -> 64 | -635 -> -4,496 | 1.43 -> 1.33 | 17,467 -> 12,017 |
| CALL Normal | 140 -> 134 | -23,809 -> -9,509 | 1.15 -> 1.25 | 10,326 -> 21,780 |
| PUT GapUp | 84 -> 80 | +31,609 -> +27,791 | 1.94 -> 1.90 | 28,177 -> 28,270 |
| PUT GapDown | 67 -> 65 | +11,264 -> +14,609 | 1.71 -> 1.81 | -251 -> -251 (5) |
| PUT Normal | 182 -> 177 | -15,069 -> -10,440 | 1.30 -> 1.33 | 28,123 -> 39,531 |

## CALL gap-up reading
- The filter removes 3 of 40 trades, all stop-outs (e.g. BANKNIFTY 2026-08-27 13:39, SENSEX 2026-09-16 09:35). That is the whole +6.7k.
- Holdout is untouched: same 9 trades, same P&L. Validation gains +4.3k from two removed losers.
- Result rests on ~9 gap-up sessions; 2026-10-09 and 2026-10-23 winners carry the base itself.
- Classification: **PROMISING, insufficient sample** (needs 30 holdout trades, has 9). Not validated.

## Other cells
- CALL Normal improves but stays net negative after costs. CALL GapDown gets worse. PUT effects are small and mixed. No consistent direction across opening types, so no global rule.

## Limits
- 3 removed trades cannot support a rule; losers removed could equally be luck.
- 1m EMA9/21 only; a 5m variant was not run.
- Stage 2 parity audit not run; nothing here is approved for live.

## Next step
Re-run once gap-up CALL holdout reaches ~30 trades, or log EMA9/EMA21 into snapshots so it can be forward-tested in paper.
