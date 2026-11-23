# Signal parity: replay vs real signals

From 2026-11-20 IST; live StrategySettings with overrides CallRsiCeiling=85; trading mode Live; BreakoutProtection OFF; selector replayed (MarketRegimeGate, Range/Reversal/NoTrade/News routing); 5m confirmation, Chop/FastEma/SessionTrend/EarlyTrend/IndexStopFloor/AtrGate replayed; stateful gates are reported, not replayed.

## Profit (Rs; backtest is gross, no brokerage/slippage)

| Total | Trades | Backtest P&L | Live P&L |
|---|---|---:|---:|
| TOTAL_BACKTEST | all replay entries: 24 trades, 13 win / 11 loss | +4,115.87 |  |
| TOTAL_BACKTEST_EXCL_STATEFUL | without entries a live position/cooldown blocked: 11 trades, 6 win / 5 loss | +2,412.61 |  |
| TOTAL_MATCH | signals both took (backtest vs live): 8 trades, 4 win / 4 loss | +1,334.92 | -56.70 |
| TOTAL_LIVE | real signals (TradeJournal): 11 trades, 6 win / 5 loss |  | +1,382.70 |

Real signals: 12; comparable (Directional, inside the window, with replay data): 12; reproduced by the replay: 8 (66.7%).

## Every status

| Status | Count |
|---|---:|
| BACKTEST_ONLY_STATEFUL | 13 |
| MATCH | 8 |
| LIVE_ONLY | 4 |
| BACKTEST_ONLY | 3 |

## Real signals the replay missed, by the replay gate that blocked them (fix these first)

| Replay BlockedBy | Count |
|---|---:|
| AndChain | 2 |
| NoTradeWindowGate | 2 |

## Replay-only entries, by the live gate that stopped them

| Live BlockedBy | Stateful (expected) | Count |
|---|---|---:|
| DuplicateSignalGate | yes | 5 |
| OnePositionGate | yes | 5 |
| StrategySelector | no | 3 |
| CircuitBreakerGate | yes | 2 |
| ConsecutiveLossGate | yes | 1 |

## Per day

| Date | Real | Reproduced | Missed | Replay-only (non-stateful) |
|---|---:|---:|---:|---:|
| 2026-11-20 | 12 | 8 | 4 | 3 |
