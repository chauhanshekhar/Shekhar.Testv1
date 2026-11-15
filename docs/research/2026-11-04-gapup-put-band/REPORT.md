# Gap-up PUT 5m RSI band, 2026-11-04

`dotnet run --project src/Trading.Backtest -- --gapup-put-band --side PUT --cost-per-order 20 --slippage-points 1 --dump-trades --no-grid --min-trades 1`
Gap-up sessions only (gap >= 0.30%). Only GapUpPutRsiFloor/Ceiling changed; live is 40-60. Net after costs. 7 cells.

| Band | Trades | Win % | Net | PF | Max DD | Train / Valid / Test net |
|---|---|---|---|---|---|---|
| Live 40-60 | 84 | 53.6 | +31,609 | 1.94 | 10,154 | 14,873 / 17,459 / 28,177 |
| 45-60 | 56 | 50.0 | +12,796 | 1.70 | 6,738 | 3,638 / 17,352 / 10,686 |
| 42-60 | 70 | 54.3 | +21,651 | 1.86 | 7,082 | 9,226 / 15,723 / 21,001 |
| 35-60 | 105 | 54.3 | +44,819 | 2.06 | 14,424 | 30,844 / 16,433 / 34,163 |
| 40-55 | 80 | 48.8 | +19,901 | 1.72 | 11,871 | 1,088 / 16,928 / 29,066 |
| 45-55 | 52 | 42.3 | +1,089 | 1.40 | 12,929 | -10,147 / 16,821 / 11,575 |
| 40-65 | 86 | 52.3 | +26,417 | 1.83 | 10,154 | 9,960 / 17,459 / 28,177 |

- Raising the floor to 45 loses 28 trades and 18.8k net; no slice improves.
- Lowering the floor to 35 adds trades and net, positive in all three slices, but drawdown rises and the band is chosen from 7 cells on the same data. Per the earlier withdrawn BANKNIFTY 32-45 result, re-check on the unfiltered base rate before treating 35-60 as a candidate.
- Nothing changed in live settings.
