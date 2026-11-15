# OPTION_SL_HIT: did the index reverse, and would a persistence rule help? (2026-10-27)

Read-only. Data: 61 SINGLE `OPTION_SL_HIT` signals, 18 sessions (2026-09-24 .. 2026-10-27), `Candles` 1-minute (UTC -> IST),
`Signals`, `TradeJournal`, entry-order units. Script: `study.py` (inputs exported with sqlcmd; see its header). Not recommendations to
change production.

## 1. Reversal past the entry level within 30 minutes (base rate first)
| | Rate |
|---|---|
| Stop-outs: index reaches the signal entry level within 30 min of the exit | **72%** (44/61) |
| Base rate: any 1-minute bar with the same adverse move over the same hold, same symbol and side, returning to its reference within 30 min | **66%** (mean over 61 matched sets, median 1,867 bars each) |
| Other exits, same measure: PROTECTED_SL_HIT 98%, INDEX_SL_HIT 50% | |

The entry level is crossed again on most bars of an oscillating index, so this measure barely separates a stop-out from an ordinary
adverse move (+6 points over base). The stricter reading, reversal BEFORE the index stop is hit: 26 of 61 (43%) reversed first, 35 of 61
(57%) hit the index stop first, so most of these stops were right.

By day: stop-outs cluster (10-07: 12, 10-22: 9, 10-08: 8). Reversed-first share per day ranges 0-100% with n of 1-4 on most days.
By symbol x side: BANKNIFTY PUT 78% (18), SENSEX PUT 88% (16), SENSEX CALL 50% (14), BANKNIFTY CALL 75% (8) vs base 57-71%.
`stopouts.csv`, `base_rate.csv`.

## 2. Persistence rule ("exit only if the index is still beyond the stop level N minutes later")
Simulated with a delta proxy, premium = OptionLtp + Delta x (index - entry), exits costed at Rs20/order + 1 point/side.
Proxy result, all 61: baseline -147,318 vs rule -78,345 (N=2), so +68,973; positive on 13 of 18 days.
**This result is not usable.** Fidelity check: at the exit minute the index was beyond the delta-implied stop level in only
**10 of 61** stop-outs (median adverse index move 27 pts against 40 pts needed). The option premium reached its stop long before delta x
index explains it (gamma, IV and spread on expiry days, intrabar spikes). The proxy therefore "releases" trades whose premium really was
below the stop. `persistence_rule*.csv`, `proxy_fidelity.csv` are kept for audit only.

## 3. Conclusions
- Stop-outs are not special against the base rate; 57% were followed by the index hitting its stop.
- A persistence rule cannot be tested on this history: option premium paths (`OptionMinuteHistory` wiped 10-07, `OptionChainData` starts
  2026-10-27 11:33) do not exist, and the delta proxy fails its own check.
- It also cannot be implemented without weakening the protective stop: single-leg stops are a resting broker SL_M (safety rules forbid
  removing or weakening them), and a paper-only delay would make paper and live diverge.
- To make it testable: retain option premium history (nightly export of `OptionChainData`, which now exists) and re-test once there are
  >= 30 stop-outs with premium paths.
