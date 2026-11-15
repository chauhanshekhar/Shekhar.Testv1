# Replay of 2026-10-29 (no live signals on a rising day)

Tool: `src/Trading.Backtest`, `--from 2026-10-29 --side CALL --cost-per-order 20 --slippage-points 1`,
`--live-interchange` (chain/) and `--rated` (rated/). Real option premiums (OptionMinuteHistory) for every trade.
Scope ALL = one shared position across indices (closest to live). Not replayed: StaleEntryGate, Risk, R:R, Spread.

## What blocked live on 10-29 (IndicatorSnapshots.BlockedBy, CE only)

NIFTY IndexStopFloorGate 7,921 (ATR 8-12 < 13.33) · SENSEX StaleEntryGate 4,880 / EarlyTrendGate 3,627 ·
BANKNIFTY OiConfirmationGate 3,746 / EarlyTrendGate 2,538 / StaleEntryGate 2,499.

## 10-29 replay, ALL scope, net after costs

| Configuration | Trades | Wins | Net Rs |
|---|---|---|---|
| Live rules | 2 | 2 | +1,993 (live took 0: StaleEntryGate, not replayed) |
| EarlyTrendGate off | 8 | 7 | +9,427 |
| RSI off | 4 | 4 | +9,074 |
| RSI + EarlyTrendGate off | 12 | 9 | +12,160 |
| Rated CALL S65-M30 (gates on) | 9 | 5 | +3,229 |

## Same toggle on full history (2026-10-29 live-interchange run, ALL scope, after costs)

| Configuration | Trades | Net after costs | PF | Validation net | Validated |
|---|---|---|---|---|---|
| Live | 45 | -4,315 | 1.17 | +1,376 | YES |
| EarlyTrendGate off | 93 | +4,024 | 1.37 | -3,829 (PF 0.90) | NO |

Per symbol, gate off after costs: BANKNIFTY +12,033 -> +13,657; SENSEX +16,545 -> -13,960; NIFTY +4,222 -> +235.

## Conclusion

10-29 was a one-sided trend day, the case EarlyTrendGate is designed to give up. Turning it off would have earned
~+7.4k more that day, but over full history it fails validation and badly hurts SENSEX. No global change recommended.
Only open candidate (unchanged from earlier research): EarlyTrendGate off for BANKNIFTY CALL only - small edge, needs paper test.
Rated CALL already replays with EarlyTrendGate and IndexStopFloorGate on, so its earlier backtest result stands.
