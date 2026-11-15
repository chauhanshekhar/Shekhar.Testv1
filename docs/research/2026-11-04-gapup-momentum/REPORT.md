# Pasted gap-up momentum setup (CALL, gap-up sessions only), 2026-11-04

`dotnet run --project src/Trading.Backtest -- --gapup-momentum --side CALL --cost-per-order 20 --slippage-points 1 --dump-trades --no-grid --min-trades 1`
Separate entry path, only when the session opened >= +0.30%. Conditions: 5m EMA20>EMA50, 1m EMA20>EMA50, price>EMA20, 5m RSI 45-85, ATR% >= 0.03, last 1m close > previous completed 5m high, 09:20-14:00, index-stop-floor gate kept. Exits = live exit model. Net is after costs.
NOT applied: VWAP (not in the replayed series; stored only since 10-09) and the screenshot's items 2-4 (cut off). Selector replay off. 4 arms tested.

| Arm | Trades | Win % | Net | PF | Train / Valid / Test net |
|---|---|---|---|---|---|
| Live rules, gap-up only (baseline) | 40 | 52.5 | +19,323 | 2.14 | 2,887 / 6,998 / 21,738 |
| Pasted setup, full | 43 | 37.2 | -12,707 | 1.05 | 1,123 / -186 / 795 |
| Same, RSI ceiling 65 | 24 | 33.3 | -11,025 | 0.82 | -7,897 / -1,482 / 5,273 |
| Same, no prev-5m-high confirmation | 46 | 43.5 | +13,829 | 1.98 | 10,929 / 2,928 / 15,931 |

## Reading
- The full setup is worse than the live rules on the same gap-up sessions: lower win rate, PF 1.05, negative after costs, validation negative.
- The "1m close above previous 5m high" confirmation is what hurts: without it the setup recovers to +13.8k, still below the live rules.
- It trades 19 sessions against 12 for the live rules; the extra days are mostly losers (07-27 -7.1k, 10-09 -9.2k where the live rules made +7.2k).
- Classification: **FAILED**. Do not implement.

## Limits
VWAP condition and the hidden "important changes" were not tested. Gap-up sample is about 20 sessions.
