# C1 / C3 through Trading.Backtest (live exit ladder, costs Rs20/order + 1pt/side) - 2026-10-28

Command: `dotnet run --project src/Trading.Backtest -- --proposed-candidates --side PUT --dump-trades --cost-per-order 20 --slippage-points 1 --min-trades 1 --out <this folder>`
Data from 2026-07-10, live StrategySettings applied to every day, real option premium only from 2026-10-07 (earlier = index move x avg delta).
Arms: Baseline = live rules on that opening type only; Replace = candidate replaces the live chain (index-stop floor, selector, no-trade window kept; live MinAtrPercent not applied);
ReplaceNoFloor = same, floor off; AndChain = live chain must also pass. "ALL" scope = one position at a time across symbols.
Use NetProfitAfterCosts (ranking.md's "net" columns are before costs).

| Arm (ALL) | Trades | Win % | PF (pre-cost) | Net after costs |
|---|---|---|---|---|
| C1 Baseline (live rules, gap-down) | 62 | 56.5 | 1.80 | +14,183 |
| C1 Replace | 44 | 47.7 | 1.56 | -100 |
| C1 ReplaceNoFloor | 48 | 56.3 | 1.89 | +5,058 |
| C1 AndChain | 0 | - | - | - |
| C3 Baseline (live rules, gap-up) | 74 | 52.7 | 1.85 | +23,765 |
| C3 Replace | 27 | 44.4 | 1.72 | +3,097 |
| C3 ReplaceNoFloor | 29 | 48.3 | 2.75 | +9,508 |
| C3 AndChain | 6 | 50.0 | 2.20 | +2,026 |

Per symbol (after costs): C1 Replace SENSEX 41 trades -4,478, BANKNIFTY 4 trades +5,098, NIFTY 0 (index-stop floor blocks it; with the floor off 20 trades -5,508).
C3 Replace SENSEX 17 trades +3,994, BANKNIFTY 10 trades -897; NIFTY 0 (floor).
Held-out test period: C1 Replace 0 trades, C3 Replace 19 trades (+1,725 PF 1.15; no-floor +8,420 PF 2.09).
Neither candidate beats the live rules restricted to the same opening type, and neither has a usable held-out sample.
