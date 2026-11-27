# Backtest ablation - 2026-11-25 only (live settings, costs Rs20/order + 1pt, --regime-replay)

Trust: parity reproduced 3 of 3 real signals (USABLE label, but n=3 for one session).
Baseline: 3 trades (all CALL), win 33.3%, net -2,498, PF 0.63, expectancy -833, max DD 6,704.
SENSEX 2 trades +2,160 (PF 2.06); NIFTY 1 trade -4,658. No PUT trades.

Ablation: see ablation.csv (compare.py computes everything). 24 switches replayed; 19 changed nothing.
Changed runs (all TOO FEW TRADES - one session, no decision possible):
- CallPrev5mHighFilter off: 4 trades, net +5,460 (-2,498 -> +5,460), but trades differ from baseline (entries shift)
- IndexStopFloor off: 2 trades, net +135 vs -2,498
- Atr5mStop off: 0 trades (blocks every entry)
- TargetLadder off: 4 trades, net -3,588 (ladder helps today)
- ChopGate@BANKNIFTY off: 2 trades (adds a BANKNIFTY winner +2,521, drops SENSEX trades)

Not testable in replay: CloseBreakoutGate, EarlyTrendSoftGate, EfficiencyRatioGate, ExpiryAutoRoll,
GapUpMorningException, OutcomeTracking, StopNoiseGate, Trend5mGate, WhipsawShadow.
Already off in DB (skipped): EnableBreakevenPartialSale, EnableConsecutiveLossGate.
Recommendation: do not change any setting from one session; re-run with --from <10+ sessions back>.
