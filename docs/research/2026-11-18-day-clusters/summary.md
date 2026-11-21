# Stop-out clustering by session - result (2026-11-18)

Command: `dotnet run --project src/Trading.Backtest -- --day-clusters --side BOTH --cost-per-order 20 --slippage-points 1 --out docs/research/2026-11-18-day-clusters`
Simulated, costed entries (live rules + unfiltered base). Tables: day-clusters.md. SL = T1 never reached and not an end-of-day exit.

## Findings
- **No excess clustering by session on the unfiltered base**: SD of daily SL share vs the independent-trade expectation = ratio 0.97 (CALL, 168 session-symbols) and 0.99 (PUT, 173). Live-rule PUT 1.15 (40 session-symbols) and live CALL 0.51 (14) are too few days to read.
- Bad sessions exist (e.g. SENSEX 09-03 and 11-18 CALL 9/10 SL) but occur about as often as chance predicts at a ~60% SL rate.
- **Hindsight only (not usable at entry)**: CALL on Normal/TrendDown days 71% SL (stable in both halves, n=285), GapDown/Continuation 73%; high full-day range days have fewer SL (54% vs 66%). These describe a day after it ended.
- **Known-in-advance features do not separate**: opening gap 60-61% CALL / 57-60% PUT in every bucket; first-hour range and efficiency flip between halves.
- Entry hour: 15:00+ entries show 35-38% SL only because they run into the end-of-day exit (NONE), not because they are better.

## Conclusion
Stop-outs are close to independent coin-flips at ~60% on the unfiltered base; neither per-entry features nor session type that is knowable at entry predicts them. No filter candidate here. This differs from the earlier "stops cluster by day" note for real trades: on the 1-3 months of simulated entries the excess is not visible.
