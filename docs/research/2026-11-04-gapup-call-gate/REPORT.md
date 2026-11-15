# Gap-up CALL, early-trend gate off, 2026-11-04

`--gapup-call-gate --side CALL --cost-per-order 20 --slippage-points 1 --dump-trades`. Gap-up sessions only; live gap-up CALL band 45-85. Net after costs. 3 cells.

| Arm | Symbol | Trades | Net | PF | Max DD | Train / Valid / Test net |
|---|---|---|---|---|---|---|
| Live | BANKNIFTY | 9 | +8,899 | 2.87 | 3,902 | -953 / 2,424 / 10,648 |
| Gate off BANKNIFTY | BANKNIFTY | 58 | +8,148 | 1.57 | 12,910 | -2,576 / -3,389 / 34,693 |
| Live | ALL | 40 | +19,323 | 2.14 | 7,060 | 2,887 / 6,998 / 21,738 |
| Gate off BANKNIFTY | ALL | 88 | +21,932 | 1.75 | 12,910 | 1,264 / 3,978 / 46,069 |
| Gate off every symbol | ALL | 130 | +20,240 | 1.68 | 9,850 | 6,490 / 8,710 / 47,200 |

- Turning the gate off for BANKNIFTY takes it from 9 to 58 trades, but BANKNIFTY net falls (+8.9k to +8.1k), profit factor drops 2.87 to 1.57 and drawdown triples.
- BANKNIFTY train and validation are both negative; the whole result comes from the last slice (+34.7k). Fails the validation requirement. **FAILED.**
- Gate off for every symbol: more trades and similar net, lower PF (1.68 vs 2.14), higher drawdown. No improvement per trade.

## Gap-up PUT by symbol, live band 40-60 (from 2026-11-04-gapup-put-band)
NIFTY n=8 net -8,429 PF 0.74; BANKNIFTY n=49 +17,692 PF 1.89; SENSEX n=43 +19,423 PF 2.31. SENSEX train has only 2 trades; its result sits in the last two slices. A BANKNIFTY-only PUT band is NOT supported: SENSEX is the strongest of the three on the live rules. The earlier base-rate view (SENSEX PF 0.90) differs from the live-rule trades; unresolved, sample is small.
