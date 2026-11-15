# Version D: gap-up morning exception to EarlyTrendGate, 2026-11-04 (research only)

`--gapup-morning-exception --side CALL --cost-per-order 20 --slippage-points 1 --dump-trades`.
A = live gate. D1/D2/D3 = a CALL on a gap-up session (open >= 0.30% above the previous close, fixed before testing) is exempt from the EarlyTrendGate before 10:00 / 10:15 / 10:30 IST; everything else live. Net after costs, ALL = shared one-position rule, per-symbol rows = each index on its own. Chronological train/validation/test as in the harness. 3 cutoffs tested, declared in advance. Live gate and settings unchanged.

## Whole strategy (all sessions, ALL symbols)
| Arm | Trades | Net | PF | Max DD | SL-first | Valid net / PF | Test net / PF |
|---|---|---|---|---|---|---|---|
| A live | 223 | -11,487 | 1.29 | 27,840 | 48% | 10,086 / 1.16 | 35,696 / 1.44 |
| D1 10:00 | 246 | +6,115 | 1.39 | 34,669 | 46% | 11,466 / 1.18 | 49,142 / 1.59 |
| D2 10:15 | 255 | +14,763 | 1.44 | 34,669 | 44% | 10,228 / 1.16 | 52,746 / 1.62 |
| D3 10:30 | 262 | +10,233 | 1.42 | 34,669 | 45% | 10,228 / 1.16 | 56,410 / 1.66 |

## Gap-up sessions only
| Arm | Trades | Net | PF | Max DD | Train net | Valid net / PF | Test net / PF |
|---|---|---|---|---|---|---|---|
| A live | 40 | +19,323 | 2.14 | 7,060 | 2,887 | 6,998 / 1.37 | 21,738 / 6.45 |
| D1 10:00 | 65 | +33,992 | 2.24 | 7,575 | 17,402 | 8,379 / 1.42 | 30,671 / 3.83 |
| D2 10:15 | 76 | +39,139 | 2.34 | 8,813 | 23,383 | 7,141 / 1.34 | 34,275 / 3.74 |
| D3 10:30 | 83 | +34,608 | 2.14 | 8,813 | 17,229 | 7,141 / 1.34 | 37,939 / 4.03 |

**Normal sessions: identical to A in every arm and every symbol (the exception cannot fire there).**

## Per symbol, gap-up sessions
| Symbol | A net (n) | D1 / D2 / D3 net (n) | Valid PF A -> D1/D2/D3 | Test net A -> D1/D2/D3 |
|---|---|---|---|---|
| NIFTY | 10,605 (3) | 12,857 (9) / 9,613 (10) / 9,613 (10) | n/a (1-3 trades) | 11,006 -> 14,287 / 11,603 / 11,603 |
| BANKNIFTY | 8,899 (9) | 28,535 (23) / 27,289 (26) / 24,567 (32) | 1.90 -> 1.90 / 1.90 / 1.90 | 10,648 -> 26,439 / 26,439 / 28,487 |
| SENSEX | 15,394 (33) | 19,888 (52) / 20,142 (62) / 21,825 (65) | 1.34 -> 1.53 / 1.44 / 1.44 | 14,234 -> 10,987 / 12,257 / 16,050 |

## Consistency (whole strategy, per day change vs A)
D1: 16 days changed, 10 better, 6 worse, total +17.6k, +10.0k without the best day. D2: 12 better, 5 worse, +26.3k (+18.6k without the best day). D3: 11 better, 6 worse, +21.7k (+14.1k without the best day). Worst single day -7,230.

## Against the acceptance criteria
1. **Validation PF improves:** only D1 (gap-up 1.37 -> 1.42; all 1.16 -> 1.18). D2 and D3 are flat or lower (1.34 vs 1.37; 1.16 vs 1.16). The exception adds only 4-5 trades to the validation slice, so this is thin.
2. **Holdout net improves:** yes in all three, but holdout PF falls (gap-up 6.45 -> 3.7-4.0, from an outlier). SENSEX holdout net falls for D1 (-3.2k) and D2 (-2.0k); BANKNIFTY (5-7 trades) carries it.
3. **Drawdown:** gap-up +7% (D1) to +25% (D2/D3); whole strategy +25% for all D arms.
4. **Consistency across sessions:** reasonable (10-12 of 16-17 changed days better; survives removing the best day).
5. **Normal day unchanged:** confirmed.

## Verdict
**PROMISING, not validated.** D1 (until 10:00) is the only arm that meets the letter of the validation criterion, by a margin (+0.05 PF) that rests on 4 added validation trades; most of the gain sits in train (+14.5k) and test slices. The hypothesis was formed after looking at the full-sample blocked-trades table, so the training gain is not independent evidence. Do not enable in live. Following the stated rule, no other time cutoff will be searched.

## Next step
Forward test in paper only: a code change (exception inside EarlyTrendGate, switch off by default) needs explicit approval. Shadow-logging "would be blocked by EarlyTrendGate but gap-up and before 10:00" on the stored snapshots needs no code change beyond what already exists (BlockedBy, DayOpen, YesterdayClose are stored).
