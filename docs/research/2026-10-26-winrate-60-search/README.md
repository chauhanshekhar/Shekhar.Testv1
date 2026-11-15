# 60% win-rate search, per symbol and side (2026-10-26)

Question: can any entry condition from the snapshot history give >60% winning trades for CALL and PUT
on each symbol, and by market type?

## Method
- `dotnet run --project src/Trading.Backtest -- --side BOTH --base-rate --dump-trades --day-types --cost-per-order 20 --slippage-points 1`
  (all entry filters off, unlimited positions: the unfiltered base rate, rebuilt from Candles; 74/61/73 sessions).
- Win = net P&L after costs > 0. Opening type from `OpenGapPct` (+/-0.30%).
- Search: every 1-, 2- and 3-feature cell over opening type, RSI 5m, RSI 1m, PCR, ATR%, day change,
  stretch from EMA20, efficiency, entry hour. Pass = n>=30, win>=60%, and >=55% (n>=10) in BOTH
  chronological halves. Control: the same search on outcomes shuffled at random (5 runs).
- Files: `trades.csv`, `grid.csv`, `cells-60pct.csv` (every passing cell).

## Result: no condition set is distinguishable from chance
| Side / symbol | Trades | Base win % | Break-even win % | Cells passing | Passing on shuffled outcomes |
|---|---|---|---|---|---|
| CALL BANKNIFTY | 580 | 36.0 | 41.4 | 0 | 0-1 |
| CALL NIFTY | 72 | 47.2 | 43.2 | 0 | 0 |
| CALL SENSEX | 789 | 38.1 | 42.0 | 2 | 0-1 |
| PUT BANKNIFTY | 856 | 42.8 | 44.7 | 5 | 1-6 |
| PUT NIFTY | 102 | 49.0 | 48.5 | 0 | 0-1 |
| PUT SENSEX | 997 | 42.6 | 43.0 | 5 | 1-5 |

The number of "60%" cells found in real data equals what random noise produces. They are also tiny
(30-60 trades, 4-27 days) and mutually unrelated (hour 10 here, hour 11 there). Do not propose them.

## Why 60% is the wrong target here
Average win is only 1.2-1.4x average loss, so break-even is 41-45%, and the unfiltered base rate sits
at 36-43% net of costs. A real 60% would need ~+17 points over base; no tested feature gives it.
Win rate can be raised mechanically (nearer target, wider stop) but that lowers the payoff and does not
raise profit factor (see memory notes `stop-width-tested`, `why-stops-measured`).

## Caveats
Single base-rate run; premium is index-move x delta where no option history exists (before 2026-10-07);
1m-close stop granularity; no gates replayed. Overlapping entries are not independent.

---
# Part 2: can the exit structure lift win rate without lowering profit factor? (no)

`exit_sweep.py` (inputs: `trades.csv` entries, `candles-1m.csv` from `pull_candles.py`; output `exit-grid.csv`,
summary `exit_summary.py`). Each base-rate entry is replayed on following 1m bars: stop 0.5-2.0 ATR, target
0.25-3 ATR or none, 30-minute or end-of-day time stop, optional breakeven lock at +0.5/+1.0 ATR (216 configs per
side/symbol). Premium = index move x delta 0.45, 4 lots, costs Rs20/order + 1pt slippage/side; a same-bar stop/target
tie counts as the stop. Halves split by date. Control: same sweep on random entry minutes in the same sessions.

| Side / symbol | Trades | Ref (stop 1.25 / target 2) win, PF | Configs with win>=60% | Best PF among them | Any with net>0 |
|---|---|---|---|---|---|
| CALL BANKNIFTY | 580 | 31.9%, 0.64 | 20 | 0.62 | 0 |
| CALL NIFTY | 72 | 45.8%, 0.97 | 22 | 0.71 | 0 |
| CALL SENSEX | 789 | 36.5%, 0.76 | 16 | 0.62 | 0 |
| PUT BANKNIFTY | 856 | 37.3%, 0.75 | 24 | 0.71 | 0 |
| PUT NIFTY | 102 | 44.1%, 0.84 | 24 | 0.82 | 0 |
| PUT SENSEX | 997 | 42.1%, 0.93 | 20 | 0.78 | 0 |

- 60% win rate is reachable only with a tiny target (0.25-0.5 ATR) and a wide stop. Every such config has PF
  0.6-0.8 and loses money after costs. Win rate rises, profit factor falls.
- Configs with PF>1 in both halves: 0 for CALL BANKNIFTY/SENSEX and PUT BANKNIFTY/SENSEX; CALL NIFTY 22 and PUT NIFTY 3
  (72 and 102 trades). The shuffled control finds 16 for PUT NIFTY and 8 for PUT BANKNIFTY, so small-sample noise.
- Caveat: this is a plain stop/target replay, harsher than the live ladder/arm/trail (reference PF here 0.64-0.93 vs
  the engine's base rates). It shows win rate and PF trade off, not the live exit's absolute PF.

---
# Part 3: VWAP conditions after 11 VWAP sessions (2026-10-09 to 2026-10-26; SENSEX 10-09 excluded as invalid)

VWAP = `IndicatorSnapshots.Vwap` (near-month future VWAP shifted by basis, index points). Distance = (Ltp - Vwap) / Ltp.
Files: `snapshots-vwap.csv`, `trades-with-vwap.csv`, `vwap-forward.csv`; scripts `pull_vwap_snapshots.py`,
`vwap_trades.py`, `vwap_forward.py`.

**A. Base-rate trades (costed) joined to the entry minute's VWAP distance: 699 trades, 11 sessions.**
Base win: CALL NIFTY 50%, BANKNIFTY 35%, SENSEX 40%; PUT NIFTY 47%, BANKNIFTY 39%, SENSEX 42%.
- No VWAP bucket reaches a stable 60%. The buckets that do (CALL just below VWAP 69% on 16 trades; PUT >0.3% below
  80% on 10 trades from ONE day) are tiny and non-monotonic: the neighbouring buckets are 25-40%.
- PUT BANKNIFTY far below VWAP (>0.1%): 108 trades, win 37%, PF 0.72, -39.0k. Stretched shorts lose, consistent with
  the earlier stretch-cap finding; the opposite of "VWAP confirms the move".
- CALL above VWAP by >0.15%: 169 trades, win 41-43%, PF 1.1-1.3 pooled but BANKNIFTY CALL there is PF 0.95.

**B. Every minute (no entry rules), forward 30-minute move by VWAP distance: 10,562 minute-rows.**
Share of minutes where price was higher 30 min later stays 35-63% in every bucket and flips sign between days
(days with positive mean 3-7 of 8-11). Best cells (NIFTY 0.15-0.30% above 61%, NIFTY 0.05-0.15% below 63%) are on
opposite sides of VWAP, so there is no directional rule. SENSEX >0.3% below: 38% up (n=922, mostly one trend day).

**Conclusion:** 11 sessions give no VWAP condition that lifts win rate to 60% or beats the base rate consistently.
Not proposed. `VwapSideGate` stays off. Re-test once ~25 sessions exist, and add a pass/fail per day.
(Some mean-ATR cells printed inf/NaN where AtrPct is 0/null in snapshots; the share-up column is unaffected.)

---
# Part 4: wide search with every available column, walk-forward model, shuffled-label control

Features (24, all known at the minute, from 1m candles for 74 sessions): EMA20/50 distance and gap in ATR, EMA20 slope,
RSI 1m and closed-bar 5m RSI, 5m EMA gap, returns over 5/15/30/60 min, day change, opening gap, position in day range,
distance from day high/low, range so far, candle body and size, up-bar share, session efficiency, minute of day, ATR %.
Label: costed net>0 for stop 1.25 ATR / target 2 ATR / end of day (Rs20 per order + 1pt slippage). Gradient boosting,
trained on past sessions only (expanding window, 1-session purge), tested on the next 6 sessions, repeated to the end
(44 test sessions). Selection = top 2/5/10% by predicted win probability, one position per symbol at a time.
Control = same pipeline with training labels shuffled (3 seeds).

| Side | Top | Trades | Days | Win % | PF | Net Rs | Shuffled control win % |
|---|---|---|---|---|---|---|---|
| CALL | 2% | 130 | 19 | 36.2 | 0.64 | -60,293 | 34-41 |
| CALL | 5% | 187 | 17 | 32.1 | 0.55 | -117,069 | 35-38 |
| CALL | 10% | 258 | 20 | 34.9 | 0.67 | -110,075 | 35-42 |
| PUT | 2% | 132 | 21 | 43.2 | 0.83 | -21,270 | 44-47 |
| PUT | 5% | 225 | 22 | 46.2 | 0.91 | -20,008 | 41-47 |
| PUT | 10% | 309 | 24 | 45.3 | 0.93 | -21,115 | 38-44 |

The model's best-ranked minutes do no better than random-label models. Only PUT SENSEX (top 5-10%: win 48-52%, PF 1.2-1.3,
17 days) is above the others, and it is weak and unconfirmed. Per symbol detail is in the run output of `walkforward_model.py`.

Adding snapshot-only columns (PCR, PCR change 15/60 min, VWAP distance, market regime, market state, confidence), on the
43 sessions where snapshots exist (CALL top 10%: win 37.0%, PF 0.76; PUT top 10%: win 42.4%, PF 0.88) also stays inside the
shuffled-control band (35-44%). Snapshot history is only ~25-45 sessions per symbol (8k-11k minute rows each).

Not available in `IndicatorSnapshots`, so untestable: OI change (only on blocked rows), volume (VWAP only since 10-09),
option premium/IV, spread.
