# Post-stop-loss diagnostic - result (2026-11-18)

Command: `dotnet run --project src/Trading.Backtest -- --post-sl-diagnostic --side BOTH --cost-per-order 20 --slippage-points 1 --out docs/research/2026-11-18-post-sl-diagnostic`
Data: 4,427 simulated, costed entries (live rules + unfiltered base), 78-91 sessions per index. Tables: diagnostic.md, per-entry rows: diagnostic.csv.
"SL" = T1 never reached and not an end-of-day exit (so it includes any non-target exit before EOD).

## Finding: no feature separates the stop-outs
Base-rate (unfiltered) SL share is 61% CALL / 59% PUT. Every diagnostic group sits within about +-3 points of that, and the sign flips between date halves:
- Previous trade SL: 61% CALL / 58% PUT, the same as after a TARGET (61% / 59%). Entries do not fail more often right after a stop.
- Low EMA gap: 61% / 58%. Large distance from EMA20: 60% / 58%. High 1m reversals: 60% / 60%. 5m reversals >= 3: 61% / 55%.
- Previous direction reversed >= 2 ATR: CALL 59% (H1 68%, H2 48% - sign flips), PUT 60%.
Net P&L differs by group, but that is trade mix and sizing, not a different stop rate; none is stable across halves.

Conclusion: consistent with earlier results (stops cluster by day; no entry feature predicts them). Do not build an entry filter from these five features. Next evidence would come from a per-DAY view (which sessions produce the stop clusters), not a per-entry feature.
