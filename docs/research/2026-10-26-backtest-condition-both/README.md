# /backtest-condition, CALL and PUT, all symbols (2026-10-26)

Engine: `Trading.Backtest` with CURRENT StrategySettings (live ladder/arm/trail exit), costs Rs20/order + 1pt, real option
premium from 2026-10-07 else index move x average delta, 700 configs/side. Inputs `snapshots.csv`, `real-signals.csv`,
`settings.csv` exported read-only. Files: `ranking.md`, `grid.csv`, `trades.csv`, `grid-tables.txt`, `feature-tables.txt`,
`real-tables.txt`, `explorer.html`. All figures are base rates over trend-direction minutes that pass the live gates, not
realised signals. Backtest and real are separate (SIMULATED vs ACTUAL CLOSED).

## Headline
| Side | Simulated (current settings) | Actual closed trades |
|---|---|---|
| CALL | 52 trades, 15 days, win 44%, PF 1.85, net +54,120 | 90 trades, 22 days, win 38%, PF 0.89, net -9,451 |
| PUT | 288 trades, 56 days, PF 1.49, net +113,058 | 152 trades, 28 days, win 47%, PF 1.26, net +32,580 |

By symbol (simulated -> actual): CALL NIFTY +20.7k (12) -> -4.4k (7); CALL BANKNIFTY +31.8k (36) -> +12.7k (31);
CALL SENSEX +37.2k (25) -> -17.7k (52). PUT BANKNIFTY +71.6k (215) -> +11.4k (67); PUT SENSEX +70.3k (107) ->
+20.4k (73); PUT NIFTY +19.0k (28) -> +0.8k (12).

Backtest is better than real everywhere, and CALL SENSEX flips sign. Likely causes: other live gates, fills, one position at a time.

## Best cells (candidates need Net>0, PF>1, enough days; none is validated)
- CALL 0 of 700 configs validated on BANKNIFTY and NIFTY; SENSEX `CALL|NoRsi+NoPcr` validated (val +3.9k PF 1.13, test +45.5k PF 2.17, 47 trades).
- PUT BANKNIFTY best validated `NoRsi+NoEmaGap`; PUT SENSEX `NoRsi+NoPcr` (val +13.3k PF 1.29, test +92.2k, 206 trades).
- CALL baseline by 5m RSI: 45-55 win 81% (21 trades, 7 days, PF 6.9); 55-65 win 28% (40, PF 0.97); >=65 70% (10). The
  55-65 band with day change >= +0.3% is 1 win in 14 (-28.8k). The REAL closed CALLs in 45-55 are 43% win, PF 0.82,
  so the backtest cell is not confirmed by real trades.
- PUT baseline: PCR >= 1.1 win 54% PF 2.28 (91 trades); RSI5 30-38 with PCR >= 1.1 win 57% PF 2.54 (51, 10 days) but 30-38 with PCR 0.9-1.1 is 38% PF 0.83.

## Caveats
Only 15 CALL days; CALL cells under ~10 days are directional only. Delta coverage is low (BlockedDelta on 9 of 73 CALL
trades). VWAP exists only from 2026-10-09. Candidate filters here were measured on a sample the live gates already
selected; per the unfiltered-base-rate rule they must be re-tested with `--base-rate` before any proposal.
