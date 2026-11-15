# Cross-index agreement filter (candidate, not implemented)

Rule: keep a signal only if BOTH other indices moved in the signal's direction over the prior 15 min
(closed 1m candles only). Result on 263 signals 08-04..10-26: stop rate 35% vs 44%, sim +0.27R vs -0.12R;
costed (Rs20/order + 1pt/side) KEEP +55k PF 1.34, BLOCK -36k PF 0.69; first half KEEP ~break-even.

Forward check (no code change needed): the feature is derivable from the `Candles` table, so new signals
can be scored after the fact. Re-run from repo root, passing an output dir as the argument:

    python docs/research/2026-10-26-cross-index-agreement/1_pull_signals.py <dir>
    python docs/research/2026-10-26-cross-index-agreement/2_features_sim.py <dir>
    python docs/research/2026-10-26-cross-index-agreement/3_validate.py <dir>
    python docs/research/2026-10-26-cross-index-agreement/4_costed.py <dir>

Needs `docs/research/2026-10-26-winrate-60-search/candles-1m.csv` refreshed (pull_candles.py there).
Judge only signals created after 2026-10-26 (out-of-sample). Re-check after ~10 sessions.
