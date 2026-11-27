# Signal parity: replay vs real signals

From 2026-11-25 IST; live StrategySettings; trading mode Paper; BreakoutProtection from StrategySettings; selector replayed (MarketRegimeGate, Range/Reversal/NoTrade/News routing); 5m confirmation, Chop/FastEma/SessionTrend/EarlyTrend/IndexStopFloor/AtrGate replayed; stateful gates are reported, not replayed.

## Profit (Rs; backtest is gross, no brokerage/slippage)

| Total | Trades | Backtest P&L | Live P&L |
|---|---|---:|---:|
| TOTAL_BACKTEST | all replay entries: 6 trades, 3 win / 3 loss | +7,698.20 |  |
| TOTAL_BACKTEST_EXCL_STATEFUL | without entries a live position/cooldown blocked: 3 trades, 1 win / 2 loss | +4,386.08 |  |
| TOTAL_MATCH | signals both took (backtest vs live): 3 trades, 1 win / 2 loss | +4,386.08 | -513.00 |
| TOTAL_LIVE | real signals (TradeJournal): 3 trades, 0 win / 3 loss |  | -513.00 |

Real signals: 3; comparable (Directional, inside the window, with replay data): 3; reproduced by the replay: 3 (100.0%).

## Every status

| Status | Count |
|---|---:|
| MATCH | 3 |
| BACKTEST_ONLY_STATEFUL | 3 |

## Real signals the replay missed, by the replay gate that blocked them (fix these first)

| Replay BlockedBy | Count |
|---|---:|

## Replay-only entries, by the live gate that stopped them

| Live BlockedBy | Stateful (expected) | Count |
|---|---|---:|
| OnePositionGate | yes | 2 |
| DuplicateSignalGate | yes | 1 |

## Per day

| Date | Real | Reproduced | Missed | Replay-only (non-stateful) |
|---|---:|---:|---:|---:|
| 2026-11-25 | 3 | 3 | 0 | 0 |
