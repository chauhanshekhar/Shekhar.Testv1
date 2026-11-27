# Signal parity: replay vs real signals

From 2026-11-01 IST; live StrategySettings; trading mode Paper; BreakoutProtection from StrategySettings; selector replayed (MarketRegimeGate, Range/Reversal/NoTrade/News routing); 5m confirmation, Chop/FastEma/SessionTrend/EarlyTrend/IndexStopFloor/AtrGate replayed; stateful gates are reported, not replayed.

**Settings changed on or after the replay start - the replay uses today's value unless overridden with --set:**

| SettingKey | Symbol | Current value | Active | UpdatedOn | Overridden |
|---|---|---:|---|---|---|
| AdaptiveLotMin | BANKNIFTY | 2.000000 | True | 2026-11-03 15:53 | NO |
| FastEmaGateSide | (global) | 1.000000 | True | 2026-11-04 02:21 | NO |
| FastEmaGateGapUpOnly | (global) | 1.000000 | True | 2026-11-04 02:21 | NO |
| EnableGapUpMorningException | (global) | 1.000000 | True | 2026-11-04 03:11 | NO |
| EnableFastEmaGate | (global) | 1.000000 | True | 2026-11-04 03:14 | NO |
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
| TOTAL_BACKTEST | all replay entries: 157 trades, 59 win / 98 loss | +92,934.29 |  |
| TOTAL_BACKTEST_EXCL_STATEFUL | without entries a live position/cooldown blocked: 88 trades, 37 win / 51 loss | +56,935.89 |  |
| TOTAL_MATCH | signals both took (backtest vs live): 13 trades, 5 win / 8 loss | +10,551.44 | -3,836.00 |
| TOTAL_LIVE | real signals (TradeJournal): 135 trades, 58 win / 77 loss |  | -16,975.70 |

Real signals: 136; comparable (Directional, inside the window, with replay data): 123; reproduced by the replay: 13 (10.6%).

## Every status

| Status | Count |
|---|---:|
| LIVE_ONLY | 110 |
| BACKTEST_ONLY | 74 |
| BACKTEST_ONLY_STATEFUL | 69 |
| MATCH | 13 |
| NOT_REPLAYED_HEROZERO | 13 |
| BACKTEST_ONLY_NO_AUDIT | 1 |

## Real signals the replay missed, by the replay gate that blocked them (fix these first)

| Replay BlockedBy | Count |
|---|---:|
| AndChain | 68 |
| Trend5mGate | 14 |
| EfficiencyRatioGate | 8 |
| CloseBreakoutGate | 7 |
| StopNoiseGate | 5 |
| ChopGate | 4 |
| StrategySelector | 3 |
| NoTradeWindowGate | 1 |

## Replay-only entries, by the live gate that stopped them

| Live BlockedBy | Stateful (expected) | Count |
|---|---|---:|
| SignalEngine | no | 49 |
| StaleEntryGate | yes | 26 |
| OnePositionGate | yes | 25 |
| EarlyTrendGate | no | 24 |
| DuplicateSignalGate | yes | 11 |
| ConsecutiveLossGate | yes | 3 |
| HandoffCooldownGate | yes | 2 |
| OiConfirmationGate | yes | 1 |
| StaleDataGate | yes | 1 |
| BreakoutProtection | no | 1 |
| (no audit row) | no | 1 |

## Per day

| Date | Real | Reproduced | Missed | Replay-only (non-stateful) |
|---|---:|---:|---:|---:|
| 2026-11-02 | 3 | 1 | 2 | 4 |
| 2026-11-03 | 5 | 0 | 3 | 10 |
| 2026-11-04 | 3 | 0 | 3 | 5 |
| 2026-11-05 | 3 | 0 | 1 | 6 |
| 2026-11-06 | 0 | 0 | 0 | 12 |
| 2026-11-09 | 10 | 0 | 8 | 9 |
| 2026-11-11 | 11 | 0 | 11 | 8 |
| 2026-11-12 | 12 | 0 | 11 | 3 |
| 2026-11-13 | 15 | 2 | 13 | 5 |
| 2026-11-16 | 9 | 0 | 9 | 0 |
| 2026-11-17 | 2 | 0 | 1 | 5 |
| 2026-11-18 | 29 | 4 | 25 | 0 |
| 2026-11-19 | 16 | 1 | 12 | 3 |
| 2026-11-20 | 12 | 2 | 10 | 0 |
| 2026-11-23 | 3 | 0 | 1 | 4 |
| 2026-11-25 | 3 | 3 | 0 | 0 |
