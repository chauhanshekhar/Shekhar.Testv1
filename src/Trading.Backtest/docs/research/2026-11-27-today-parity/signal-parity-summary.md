# Signal parity: replay vs real signals

From 2026-11-27 IST; live StrategySettings; trading mode Paper; BreakoutProtection from StrategySettings; selector replayed (MarketRegimeGate, Range/Reversal/NoTrade/News routing); 5m confirmation, Chop/FastEma/SessionTrend/EarlyTrend/IndexStopFloor/AtrGate replayed; stateful gates are reported, not replayed.

## Profit (Rs; backtest is gross, no brokerage/slippage)

| Total | Trades | Backtest P&L | Live P&L |
|---|---|---:|---:|
| TOTAL_BACKTEST | all replay entries: 12 trades, 10 win / 2 loss | +38,993.10 |  |
| TOTAL_BACKTEST_EXCL_STATEFUL | without entries a live position/cooldown blocked: 7 trades, 6 win / 1 loss | +20,371.47 |  |
| TOTAL_MATCH | signals both took (backtest vs live): 3 trades, 2 win / 1 loss | +136.53 | -2,076.00 |
| TOTAL_LIVE | real signals (TradeJournal): 3 trades, 1 win / 2 loss |  | -2,076.00 |

Real signals: 3; comparable (Directional, inside the window, with replay data): 3; reproduced by the replay: 3 (100.0%).

## Every status

| Status | Count |
|---|---:|
| BACKTEST_ONLY_STATEFUL | 5 |
| BACKTEST_ONLY | 4 |
| MATCH | 3 |

## Real signals the replay missed, by the replay gate that blocked them (fix these first)

| Replay BlockedBy | Count |
|---|---:|

## Replay-only entries, by the live gate that stopped them

| Live BlockedBy | Stateful (expected) | Count |
|---|---|---:|
| CrossIndexAgreementGate | no | 4 |
| OnePositionGate | yes | 3 |
| StaleEntryGate | yes | 2 |

## Per day

| Date | Real | Reproduced | Missed | Replay-only (non-stateful) |
|---|---:|---:|---:|---:|
| 2026-11-27 | 3 | 3 | 0 | 4 |
