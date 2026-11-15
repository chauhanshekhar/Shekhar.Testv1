# CALL hypothesis C: opening/entry PCR x Put/Call OI change (2026-11-05)
Read-only pull (IndicatorSnapshots, Candles, OptionMinuteHistory) 10-07..11-04, 20 sessions; train 12 / val 4 / hold 4 days.
Scripts: pull.py, analyze.py; output results.txt/run.log. Outcome = index-level R (stop 1.25xATR, target 1.2R, 45 min), NO costs,
NOT the live exit ladder. OI change = 5 strikes nearest spot 15 min earlier, past data only. snap.csv deleted (130 MB, re-pull with pull.py).
Base (all minutes): ExpR -0.004, PF 0.99.
- PCR rising alone: ExpR +0.015, PF 1.03 (train negative). PCR rising with Call OI UP (not bullish): PF 0.90, GapUp PF 0.73.
  -> confirms pasted warning: rising PCR is only informative when OI confirms.
- Put OI up & Call OI down: PF 1.06; + underlying rising: PF 1.08.
- Full C (low open PCR & rising & Put OI up & Call OI down & Ltp rising): n=226, 15 days, ExpR +0.168, PF 1.36; train +0.193, val +0.052, hold +0.206 (n=62, 4 days).
  GapUp PF 1.24, Normal 1.76, GapDown 0.84 (n=17). 12 cells tested, no multiple-testing correction.
Classification: PROMISING (holdout covers 4 days < 5 required; holdout n=62; uncosted). Not validated, not a live edge.
Next: costs in R, rerun with live exit ladder via Trading.Backtest, forward shadow test; re-run after ~10 more sessions.
