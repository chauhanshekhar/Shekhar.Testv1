# Signal parity: replay vs real signals

From 2026-11-09 IST; live StrategySettings; trading mode Paper; BreakoutProtection from StrategySettings; selector replayed (MarketRegimeGate, Range/Reversal/NoTrade/News routing); 5m confirmation, Chop/FastEma/SessionTrend/EarlyTrend/IndexStopFloor/AtrGate replayed; stateful gates are reported, not replayed.

**Settings changed on or after the replay start - the replay uses today's value unless overridden with --set:**

| SettingKey | Symbol | Current value | Active | UpdatedOn | Overridden |
|---|---|---:|---|---|---|
| EnableT2Partial | NIFTY | 1.000000 | True | 2026-11-11 05:19 | NO |
| EnableT2Partial | (global) | 1.000000 | True | 2026-11-11 05:20 | NO |
| EnableOiConfirmationGate | BANKNIFTY | 0.000000 | True | 2026-11-11 05:53 | NO |
| EnableEarlyTrendSoftGate | BANKNIFTY | 1.000000 | True | 2026-11-11 06:27 | NO |
| EnableEarlyTrendSoftGate | SENSEX | 1.000000 | True | 2026-11-11 06:27 | NO |
| ScalpingPaperOnly | (global) | 0.000000 | False | 2026-11-12 08:42 | NO |
| EnableChopGate | (global) | 1.000000 | True | 2026-11-13 08:42 | NO |
| EnableExpiryAutoRoll | (global) | 1.000000 | True | 2026-11-15 12:24 | NO |
| EnableChopGate | BANKNIFTY | 1.000000 | True | 2026-11-18 06:57 | NO |
| EnableWhipsawShadow | (global) | 1.000000 | True | 2026-11-19 03:34 | NO |
| AdaptiveLotMin | SENSEX | 2.000000 | True | 2026-11-23 05:38 | NO |
| EnableBreakoutProtection | (global) | 1.000000 | False | 2026-11-23 10:19 | NO |
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
| TOTAL_BACKTEST | all replay entries: 162 trades, 59 win / 103 loss | +49,063.42 |  |
| TOTAL_BACKTEST_EXCL_STATEFUL | without entries a live position/cooldown blocked: 108 trades, 40 win / 68 loss | +52,026.88 |  |
| TOTAL_MATCH | signals both took (backtest vs live): 24 trades, 8 win / 16 loss | +19,582.88 | -2,165.60 |
| TOTAL_LIVE | real signals (TradeJournal): 124 trades, 52 win / 72 loss |  | -16,962.60 |

Real signals: 125; comparable (Directional, inside the window, with replay data): 116; reproduced by the replay: 24 (20.7%).

## Every status

| Status | Count |
|---|---:|
| LIVE_ONLY | 92 |
| BACKTEST_ONLY | 80 |
| BACKTEST_ONLY_STATEFUL | 54 |
| MATCH | 24 |
| NOT_REPLAYED_HEROZERO | 9 |
| BACKTEST_ONLY_NO_AUDIT | 4 |

## Real signals the replay missed, by the replay gate that blocked them (fix these first)

| Replay BlockedBy | Count |
|---|---:|
| Trend5mGate | 28 |
| CloseBreakoutGate | 18 |
| StopNoiseGate | 14 |
| EfficiencyRatioGate | 11 |
| StrategySelector | 7 |
| ChopGate | 7 |
| MarketRegimeGate | 5 |
| AndChain | 2 |

## Replay-only entries, by the live gate that stopped them

| Live BlockedBy | Stateful (expected) | Count |
|---|---|---:|
| SignalEngine | no | 42 |
| EarlyTrendGate | no | 24 |
| StaleEntryGate | yes | 21 |
| OnePositionGate | yes | 18 |
| AtrGate | no | 13 |
| DuplicateSignalGate | yes | 5 |
| ConsecutiveLossGate | yes | 5 |
| (no audit row) | no | 4 |
| OiConfirmationGate | yes | 3 |
| HandoffCooldownGate | yes | 2 |
| BreakoutProtection | no | 1 |

## Per day

| Date | Real | Reproduced | Missed | Replay-only (non-stateful) |
|---|---:|---:|---:|---:|
| 2026-11-09 | 10 | 0 | 8 | 9 |
| 2026-11-11 | 11 | 0 | 11 | 6 |
| 2026-11-12 | 12 | 1 | 10 | 7 |
| 2026-11-13 | 15 | 2 | 13 | 10 |
| 2026-11-16 | 9 | 0 | 9 | 7 |
| 2026-11-17 | 2 | 0 | 1 | 18 |
| 2026-11-18 | 29 | 8 | 21 | 2 |
| 2026-11-19 | 16 | 5 | 8 | 6 |
| 2026-11-20 | 12 | 3 | 9 | 3 |
| 2026-11-23 | 3 | 0 | 1 | 6 |
| 2026-11-25 | 3 | 3 | 0 | 6 |
| 2026-11-26 | 3 | 2 | 1 | 0 |
