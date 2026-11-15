# SENSEX morning liquidity block — backtest (2026-11-02)

**Question:** `LiquidityMinVolume["SENSEX"] = 500,000` is checked against cumulative day option volume, so
SENSEX is "No liquid ATM CE/PE strikes" most mornings (5,133 snapshots on 11-02, all before 13:00). Would
those blocked entries have made money?

**Method:** `Trading.Backtest --live-interchange --selector stored --from 2026-10-07 --cost-per-order 20
--slippage-points 1 --dump-trades` (live entry rules + live exit chain on OptionMinuteHistory premiums; the
replay does not apply the liquidity filter). Live-config SENSEX trades were tagged by whether the stored
SENSEX snapshot at the entry minute had `BlockedReason LIKE 'No liquid ATM%'` (`tag_liquidity.py`).
18 sessions.

| Side | Bucket | N | Days | Net (after costs) | PF | Net ex best day |
|---|---|---|---|---|---|---|
| CALL | blocked | 1 | 1 | +5,311 | – | 0 |
| CALL | allowed | 10 | 4 | -1,875 | 0.87 | -3,934 |
| PUT | blocked | 8 | 4 | +795 | 1.13 | -2,220 |
| PUT | allowed | 43 | 11 | +12,495 | 1.36 | -1,655 |

Blocked PUT by period: train 5 trades / +915, holdout 3 trades / -120 (PF 0.96). No validation-period PUT.

**Verdict:** no historically validated edge. Nine trades in 18 sessions are far below the N >= 20-30 the pass
rule needs; the blocked PUTs are negative without their best day and in holdout; the CALL result is one
trade. Lowering the threshold is not supported by evidence — and not shown to be harmful either.

**Limits:** the replay picks its own strike/premium, not what the live selector would choose with a lower
threshold; OptionChainData is pruned, so per-minute volumes at the blocked minutes cannot be reconstructed;
costs = Rs20/order + 1 pt slippage, no STT/exchange charges.

## All symbols (follow-up)

`tag_all_gates.py`: every live-config replay trade, tagged by the live gate at its entry minute.
Morning (< 13:00 IST) replay result after costs: SENSEX 36 trades / 8 days / +22,166 (PF 1.74, +12,142 ex
best day); BANKNIFTY 33 / 12 / -13,502 (PF 0.74); NIFTY 15 / 6 / -21,076 (PF 0.34). NIFTY and BANKNIFTY morning
blocks mostly avoided losers; SENSEX blocks (NoLiquidATM, DuplicateSignalGate) mostly removed winners.
Not split-validated; 18 sessions.
