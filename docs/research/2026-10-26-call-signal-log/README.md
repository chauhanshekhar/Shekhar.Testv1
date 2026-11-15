# Why real CALLs in RSI5 45-55 lose while the replay wins (2026-10-26)

`signals.csv` = replay signal log (CALL, all 100 configs). Real trades from `../2026-10-26-backtest-condition-both/real-signals.csv`.
`--parity` crashed (`ParityCheck.cs:44`, duplicate snapshot key `30-07-2026 15:13`); not fixed (src change needs approval).

Findings (23 real CALLs in 5m RSI 45-55 since 2026-09-30, real net -4,495):
1. The replay only runs the `AND_CHAIN` entry path with selector `Ignore`. Live also trades via the strategy selector
   (`MarketState` Scalping/Trend/Reversal/Range/Breakout). 11 of the 23 real entries have no replay signal at all.
2. Replay gates that blocked trades live took: `EarlyTrendGate` (#98, #163; live had it only from 10-08), `OnePositionGate`, `DuplicateSignalGate`.
3. Matched trades exit worse live (protected stop on live option quotes vs 1m closes in the replay).
4. Real CALLs by 5m EMA alignment (71 trades with snapshots): 5m bearish (EMA20<=EMA50) n=38, win 34%, PF 0.74, -11,094;
   5m bullish n=33, win 36%, PF 1.20, +5,204. SENSEX CALL with 5m bearish: 19 trades, 5 days, win 16%, PF 0.19, -17,944.
   By MarketState: Range -4.5k (9), Reversal -4.3k (5), NoTrade -3.4k (1), Scalping +0.8k (36), Trend -4.1k (30), Breakout +3.1k (7).
   Directional only (5-10 days per cell); not a proposal.
