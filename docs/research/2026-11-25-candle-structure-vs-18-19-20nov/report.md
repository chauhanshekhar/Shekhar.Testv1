# 25 Nov candle structure vs 18 / 19 / 20 Nov

Read-only analysis (SELECTs only). Sources: `Candles` (1m index bars, UTC -> IST), `Signals`
(`BrokerOrderId <> ''`), `TradeJournal` (P&L summed per signal; partial exits create two journal rows).
No gate or setting was changed.

## Verdict
Nothing in today's data shows an improvement in entry quality, and nothing shows a target hit.

- All 3 trades today exited `PROTECTED_SL_HIT` for 0 / -237 / -276 (net -513). Peak option moves
  were 39.0 / 18.9 / 57.2 pts = **0.61 / 0.29 / 0.87 of the T1 distance** (64.1 / 65.8 / 65.4 pts).
  The 60+ pt T1 was never reached; the best trade got 57 of 65 pts and still booked a loss.
- n = 3. Too few to say anything about a gate.

## Day structure (1m index candles, IST)
| Day | Index | Net open->close | Range | Efficiency* | Close in day range | High / low made |
|---|---|---|---|---|---|---|
| 18 Nov | BANKNIFTY | -165 | 593 | 0.02 | 0.46 | high 09:17, low 09:46 (down-chop) |
| 19 Nov | BANKNIFTY | +982 | 1102 | 0.17 | 1.00 | low 09:15, high 15:29 (trend up) |
| 20 Nov | BANKNIFTY | +1979 | 2074 | 0.21 | 0.98 | low 09:15, high 15:06 (strong trend up) |
| 25 Nov | BANKNIFTY | +295 | 882 | 0.03 | 0.51 | high 11:46, low 13:16 (rally, then -700 fade) |
| 25 Nov | SENSEX | +501 | 1084 | 0.05 | 0.48 | high 11:46 |

*|net| / sum of 1m close-to-close moves. 25 Nov looks like 18 Nov (choppy, closes mid-range), not like
19/20 Nov (persistent trend, close at the high). The three CE entries (10:56-11:09) came at the
day's high, 35-50 min before it was made, and the index then gave back the whole rally.

## Entries
Distance below the prior 30-min high, as a fraction of that 30-min range (0 = at the high):
today 0.00-0.05; 19 Nov CEs 0.01-0.38; 20 Nov CEs 0.00-0.35. Today's entries were **not more
breakout-like** than the 19/20 Nov CE entries; all of them sit at the day high (day-range position
0.99-1.00, same as 20 Nov at 0.97-1.00). The earlier days also had CEs near the 30m high.

## Exits / outcomes (signals, P&L summed per signal)
| Day | Signals | P&L | OPTION_SL_HIT | PROTECTED_SL_HIT | Reached T1 distance |
|---|---|---|---|---|---|
| 18 Nov | 29 | -11,021 | 66% | 31% | 17% |
| 19 Nov | 16 | -4,953 | 44% | 31% | 25% |
| 20 Nov | 11 closed (+1 with no journal row) | +1,383 | 92% | 0% | 50% |
| 25 Nov | 3 | -513 | 0% | 100% | 0% |

20 Nov is the only day that was a genuine trend-continuation win (BANKNIFTY CE only; index +1979).
Today's lack of full stop-outs came from the exit mode, not from trend continuation.

## Not comparable: the option contract changed
- 20 Nov BANKNIFTY CE: `BANKNIFTY26NOV63400CE ...`, premium 348-449, stop distance 21-33, T1 distance 23-39.
- 25 Nov BANKNIFTY CE: **`BANKNIFTY26DEC64000CE`**, premium 1580, stop 54.9, T1 65.8.
- SENSEX premiums were also higher today (473 / 532 vs 295-350 on 18 Nov); I did not verify the SENSEX
  expiry symbol.

After the Nov monthly expiry the entries moved to the next series, so stop and T1 distances are about
2-3x larger than on 18-20 Nov. MFE in points, `PROTECTED_SL_HIT` frequency and "reached T1" all change
meaning with that. Compare in R / % of premium, not points.

## Caveats
- NIFTY and SENSEX have **no candles for 20 Nov**, so only BANKNIFTY structure is comparable for that day.
- 20 Nov 14:02 BANKNIFTY CE (`Signals.Id` in data) has no exit and no journal row; excluded from P&L.
- `Signals.OptionSL` is overwritten when the stop is moved, so risk was taken from T1 distance, not `OptionSL`.
- Index candles are 1m closes; 30-min lookbacks use bars before the entry minute (no look-ahead).

## Conclusion / next step
Do not change any gate on this. To answer the real question (trend vs breakout vs exit behaviour),
the next check is the same measurement on the post-expiry Dec-series entries only, in R-multiples,
once more than a handful exist, plus a look at why a trade with MFE 57 of 65 pts (SENSEX, 11:09)
closed at -276 on the protected stop.
