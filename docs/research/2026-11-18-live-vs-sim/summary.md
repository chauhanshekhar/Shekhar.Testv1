# Live vs simulator execution diagnostic (2026-11-18)

Data (read-only): 280 real signals since 2026-10-07 (220 paper `PAPE`, 60 broker `GMKF`, all broker ones in November) with their Orders and TradeJournal rows; 559 simulated live-rule trades from `--sim-trades` (costed). Files: real-signals.csv, sim-trades.csv, matched.csv.

## What can and cannot be measured
- **Entry slippage: not measurable from the database.** All 280 entry fills equal the signal's OptionLtp exactly (Orders.AvgPrice = Signals.OptionLtp), including the 60 broker orders. The recorded fill is the signal price, not Groww's trade price. Need Groww's order/trade book (or position credit price) to measure it.
- **Latency:** signal -> order placed: mean 0.2 s, max 1 s. Order -> recorded fill: broker mean 3.1 s (max 6), paper 2.6 s (synthetic delay).
- **Stop-loss execution (52 broker SL_M fills, all November):** average fill 0.89 premium points below trigger (-0.16%), worst -5.05. For the 24 initial (untrailed) stops the realised loss averages 1.045 R, worst 1.158 R: stops cost about 4-5% more than the planned 1 R. 26 of 52 stop exits were protected/trailed stops above the planned SL.
- Paper stop exits leave no stop order rows (ExitWorker simulates them), so stop slippage exists for broker trades only.

## Simulator vs real (96 of 280 signals matched on symbol, side, entry within 3 min)
- Win rate: real 45.8% vs simulator 44.8%; win/loss agrees on 70% of matched trades.
- Exit mix differs: of 46 real PROTECTED_SL_HIT exits, 17 were OPTION_SL_HIT in the simulator and 8 TRAILING_SL_HIT. The simulator's stop ladder does not reproduce the live protected-stop behaviour trade by trade.
- Entry premium: simulator minus real mean +6.8, mean absolute 23.5 points, so the matches are loose (bar-close vs second-level entry) and the pair comparison carries error.
- P&L is NOT comparable yet: real journal -7,376 vs simulator +44,140 on the 96 matched trades. Real trades use 4 lots at the journal's quantity; the simulator's sizing, costs and exit ladder differ. Normalise to R (or per-lot) before concluding anything from this gap.

## Limits
Only 96 matched pairs; 146 simulator entries in the window have no real counterpart (one-position rule, gates and data timing differ). Conclusions here are about measurement gaps and stop execution, not a verdict that execution explains the whole difference.

## Next evidence needed
1. Groww trade book for the 60 broker entries (true entry slippage) and for target/partial exits.
2. Per-trade R for both sides (real: journal P&L / (planned risk x quantity); simulator: PnlRs / risk), then compare distributions.
3. Why the simulator hits OPTION_SL where live shows PROTECTED_SL (breakeven arm timing at 1-min bars vs ticks).
