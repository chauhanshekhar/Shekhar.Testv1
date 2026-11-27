# Signal parity: replay vs real signals

From 2026-11-26 IST; live StrategySettings; trading mode Paper; BreakoutProtection from StrategySettings; selector replayed (MarketRegimeGate, Range/Reversal/NoTrade/News routing); 5m confirmation, Chop/FastEma/SessionTrend/EarlyTrend/IndexStopFloor/AtrGate replayed; stateful gates are reported, not replayed.

## Profit (Rs; backtest is gross, no brokerage/slippage)

| Total | Trades | Backtest P&L | Live P&L |
|---|---|---:|---:|
| TOTAL_BACKTEST | all replay entries: 12 trades, 3 win / 9 loss | +6,969.77 |  |
| TOTAL_BACKTEST_EXCL_STATEFUL | without entries a live position/cooldown blocked: 2 trades, 1 win / 1 loss | +10,927.23 |  |
| TOTAL_MATCH | signals both took (backtest vs live): 2 trades, 1 win / 1 loss | +10,927.23 | +4,622.50 |
| TOTAL_LIVE | real signals (TradeJournal): 3 trades, 1 win / 2 loss |  | +4,531.50 |

Real signals: 3; comparable (Directional, inside the window, with replay data): 3; reproduced by the replay: 2 (66.7%).

## Every status

| Status | Count |
|---|---:|
| BACKTEST_ONLY_STATEFUL | 10 |
| MATCH | 2 |
| LIVE_ONLY | 1 |

## Real signals the replay missed, by the replay gate that blocked them (fix these first)

| Replay BlockedBy | Count |
|---|---:|
| StopNoiseGate | 1 |

## Replay-only entries, by the live gate that stopped them

| Live BlockedBy | Stateful (expected) | Count |
|---|---|---:|
| OnePositionGate | yes | 9 |
| HandoffCooldownGate | yes | 1 |

## Per day

| Date | Real | Reproduced | Missed | Replay-only (non-stateful) |
|---|---:|---:|---:|---:|
| 2026-11-26 | 3 | 2 | 1 | 0 |
