# Stop-loss trades vs other real trades, with entry state (2026-10-26)

209 of 242 real closed trades (Signals + TradeJournal summed per SignalId, HERO_ZERO excluded) matched to the last
IndicatorSnapshots minute within 3 min; 79 are stop-loss exits (OPTION_SL_HIT / INDEX_SL_HIT).
`trades-with-entry-state.csv` has one row per trade: entry/stop/exit prices and times, 5m and 1m EMA gaps (in ATR),
5m RSI, PCR, ATR%, stretch from EMA20, market state, EMA20/50 flips in the 10 minutes after entry. `analyze.py` rebuilds it.

Result: none of the entry-time features separates stops from other trades (all AUC within ~1.5 SE of 0.5 except
CALL stretch 0.36, ~2 SE with 9 features x 2 sides tested). Stops are fast (median 1.5 min PUT), faster than a 1m or 5m
EMA can react, so EMA-direction change after entry cannot be the cause. Directional only: 51 PUT / 28 CALL stops, one month.
