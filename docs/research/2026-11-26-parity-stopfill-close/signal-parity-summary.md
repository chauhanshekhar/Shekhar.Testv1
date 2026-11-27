# Signal parity: replay vs real signals

From 2026-11-24 IST; live StrategySettings; trading mode Paper; BreakoutProtection from StrategySettings; selector replayed (MarketRegimeGate, Range/Reversal/NoTrade/News routing); 5m confirmation, Chop/FastEma/SessionTrend/EarlyTrend/IndexStopFloor/AtrGate replayed; stateful gates are reported, not replayed.

**Settings changed on or after the replay start - the replay uses today's value unless overridden with --set:**

| SettingKey | Symbol | Current value | Active | UpdatedOn | Overridden |
|---|---|---:|---|---|---|
| EnableCloseBreakoutGate | (global) | 0.000000 | True | 2026-11-24 08:05 | NO |
| BreakoutLookbackCandles | (global) | 5.000000 | True | 2026-11-24 08:05 | NO |
| EnableAtr5mStop | (global) | 1.000000 | True | 2026-11-24 08:05 | NO |
| Atr5mStopMultiplier | (global) | 1.000000 | True | 2026-11-24 08:05 | NO |
| EnableStopNoiseGate | (global) | 1.000000 | True | 2026-11-24 08:05 | NO |
| StopNoiseMinRatio | (global) | 1.000000 | True | 2026-11-24 08:05 | NO |
| StopNoiseBars | (global) | 6.000000 | True | 2026-11-24 08:05 | NO |
| EnableEfficiencyRatioGate | (global) | 1.000000 | True | 2026-11-24 08:05 | NO |
| MinEfficiencyRatio | (global) | 0.300000 | True | 2026-11-24 08:05 | NO |
| EfficiencyRatioBars | (global) | 6.000000 | True | 2026-11-24 08:05 | NO |
| EnableMaxDailyTradesGate | (global) | 1.000000 | True | 2026-11-24 08:05 | NO |
| MaxTradesPerDay | (global) | 6.000000 | True | 2026-11-24 08:05 | NO |
| Target2RMultiple | (global) | 2.000000 | True | 2026-11-24 08:05 | NO |
| EnableTrend5mGate | (global) | 1.000000 | True | 2026-11-24 08:05 | NO |
| Trend5mGateSide | (global) | 0.000000 | True | 2026-11-24 08:05 | NO |
| Trend5mRequirePriceSide | (global) | 0.000000 | True | 2026-11-24 08:05 | NO |
| EnableOpeningWarmupGate | (global) | 0.000000 | True | 2026-11-24 08:05 | NO |
| OpeningWarmupEndMinute | (global) | 585.000000 | True | 2026-11-24 08:05 | NO |
| EnableConsecutiveLossGate | (global) | 0.000000 | True | 2026-11-24 11:50 | NO |
| CloseBreakoutGateSide | (global) | 2.000000 | True | 2026-11-24 13:08 | NO |
| EnableCloseBreakoutGate | BANKNIFTY | 1.000000 | True | 2026-11-24 13:08 | NO |
| EnableCrossIndexAgreementGate | (global) | 1.000000 | True | 2026-11-24 13:16 | NO |
| CrossIndexLookbackMinutes | (global) | 15.000000 | True | 2026-11-24 13:16 | NO |
| CrossIndexMinAgree | (global) | 2.000000 | True | 2026-11-24 13:16 | NO |
| CrossIndexAgreementSide | (global) | 0.000000 | True | 2026-11-24 13:16 | NO |

## Profit (Rs; backtest is gross, no brokerage/slippage)

| Total | Trades | Backtest P&L | Live P&L |
|---|---|---:|---:|
| TOTAL_BACKTEST | all replay entries: 25 trades, 8 win / 17 loss | +5,662.65 |  |
| TOTAL_BACKTEST_EXCL_STATEFUL | without entries a live position/cooldown blocked: 15 trades, 6 win / 9 loss | +14,310.24 |  |
| TOTAL_MATCH | signals both took (backtest vs live): 5 trades, 2 win / 3 loss | +13,977.20 | +4,109.50 |
| TOTAL_LIVE | real signals (TradeJournal): 6 trades, 1 win / 5 loss |  | +4,018.50 |

Real signals: 6; comparable (Directional, inside the window, with replay data): 6; reproduced by the replay: 5 (83.3%).

## Every status

| Status | Count |
|---|---:|
| BACKTEST_ONLY_STATEFUL | 10 |
| BACKTEST_ONLY | 6 |
| MATCH | 5 |
| BACKTEST_ONLY_NO_AUDIT | 4 |
| LIVE_ONLY | 1 |

## Real signals the replay missed, by the replay gate that blocked them (fix these first)

| Replay BlockedBy | Count |
|---|---:|
| StopNoiseGate | 1 |

## Replay-only entries, by the live gate that stopped them

| Live BlockedBy | Stateful (expected) | Count |
|---|---|---:|
| OnePositionGate | yes | 9 |
| SignalEngine | no | 6 |
| (no audit row) | no | 4 |
| HandoffCooldownGate | yes | 1 |

## Per day

| Date | Real | Reproduced | Missed | Replay-only (non-stateful) |
|---|---:|---:|---:|---:|
| 2026-11-25 | 3 | 3 | 0 | 6 |
| 2026-11-26 | 3 | 2 | 1 | 0 |
