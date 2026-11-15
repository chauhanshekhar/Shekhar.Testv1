# Stop x target width grid (live entries, live exit ladder, Rs20/order + 1pt/side) - 2026-10-28

`dotnet run --project src/Trading.Backtest -- --exit-geometry --side BOTH --dump-trades --cost-per-order 20 --slippage-points 1 --min-trades 1 --out <this folder>`
Arms: stop {1.0, 1.1, 1.25(live)} x T1 {1.5(live), 2.0, 3.0} ATR, T2 = 1.3 x T1. Entries and the index-stop floor are held at live values.
Use NetProfitAfterCosts from grid.csv (ranking.md net is before costs). Full-sample, 2026-07-10 onward; real option premium only from 2026-10-07.

Net after costs, by symbol (live arm 1.25/1.5 -> widest-target arm 1.25/3.0 -> tight stop + wide target 1.0/3.0):
- SENSEX CALL 30,284 -> 42,042 -> 34,449; SENSEX PUT 40,011 -> 60,985 -> 57,689
- BANKNIFTY CALL 18,556 -> 13,055 -> 20,845 (best 1.0/1.5: 34,397); BANKNIFTY PUT -8,865 -> 8,566 -> 12,557 (best 1.1/3.0: 20,892)
- NIFTY CALL 7,831 -> 5,658 -> 12,624; NIFTY PUT -3,537 -> -3,424 -> 3,699 (30 trades only)
Stop-out rate stays 35-52% in every arm. The 20-point minimum stop binds for NIFTY and SENSEX (ATR ~6 and ~17 pts), so the stop multiplier
mostly moves BANKNIFTY. Results are mixed by symbol and side; no single geometry wins everywhere. Not validated on real option premiums.
