# Signal parity: replay vs real signals

From 2026-11-16 IST; live StrategySettings; trading mode Live; BreakoutProtection ON; selector replayed (MarketRegimeGate, Range/Reversal/NoTrade/News routing); 5m confirmation, Chop/FastEma/SessionTrend/EarlyTrend/IndexStopFloor/AtrGate replayed; stateful gates are reported, not replayed.

**Settings changed on or after the replay start - the replay uses today's value unless overridden with --set:**

| SettingKey | Symbol | Current value | Active | UpdatedOn | Overridden |
|---|---|---:|---|---|---|
| EnableChopGate | BANKNIFTY | 1.000000 | True | 2026-11-18 06:57 | NO |
| EnableConsecutiveLossLivePause | (global) | 1.000000 | True | 2026-11-18 08:27 | NO |
| ConsecutiveLossLivePauseCount | (global) | 3.000000 | True | 2026-11-18 08:27 | NO |
| EnableCrossIndexAgreementGate | (global) | 1.000000 | True | 2026-11-18 08:49 | NO |
| EnableWhipsawShadow | (global) | 1.000000 | True | 2026-11-19 03:34 | NO |
| AdaptiveLotMin | NIFTY | 2.000000 | True | 2026-11-19 04:37 | NO |

## Profit (Rs; backtest is gross, no brokerage/slippage)

| Total | Trades | Backtest P&L | Live P&L |
|---|---|---:|---:|
| TOTAL_BACKTEST | all replay entries: 20 trades, 8 win / 12 loss | -1,310.91 |  |
| TOTAL_BACKTEST_EXCL_STATEFUL | without entries a live position/cooldown blocked: 10 trades, 4 win / 6 loss | -2,300.95 |  |
| TOTAL_MATCH | signals both took (backtest vs live): 3 trades, 0 win / 3 loss | -1,774.20 | -453.60 |
| TOTAL_LIVE | real signals (TradeJournal): 67 trades, 30 win / 37 loss |  | -12,759.50 |

Real signals: 68; comparable (Directional, inside the window, with replay data): 64; reproduced by the replay: 3 (4.7%).

## Every status

| Status | Count |
|---|---:|
| LIVE_ONLY | 61 |
| BACKTEST_ONLY_STATEFUL | 10 |
| BACKTEST_ONLY | 7 |
| NOT_REPLAYED_HEROZERO | 4 |
| MATCH | 3 |

## Real signals the replay missed, by the replay gate that blocked them (fix these first)

| Replay BlockedBy | Count |
|---|---:|
| AndChain | 40 |
| BreakoutProtection | 21 |

## Replay-only entries, by the live gate that stopped them

| Live BlockedBy | Stateful (expected) | Count |
|---|---|---:|
| SignalEngine | no | 5 |
| StaleEntryGate | yes | 4 |
| DuplicateSignalGate | yes | 3 |
| OnePositionGate | yes | 3 |
| EarlyTrendGate | no | 2 |

## Per day

| Date | Real | Reproduced | Missed | Replay-only (non-stateful) |
|---|---:|---:|---:|---:|
| 2026-11-16 | 9 | 0 | 9 | 0 |
| 2026-11-17 | 2 | 0 | 1 | 6 |
| 2026-11-18 | 29 | 0 | 29 | 0 |
| 2026-11-19 | 16 | 0 | 13 | 1 |
| 2026-11-20 | 12 | 3 | 9 | 0 |
