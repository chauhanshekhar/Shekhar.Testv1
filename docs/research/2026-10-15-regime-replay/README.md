# What the MarketRegime gate + StrategySelector cost (2026-10-15)

Read-only. Nothing was written to the database and no setting was changed.

Same entry rules, same exit ladder, same settings, same 68 sessions as
`docs/research/2026-10-15-backtest-condition-both` — the only difference is that this run also
applies the **MarketRegime gate and the strategy selector**:

```bash
dotnet run --project src/Trading.Backtest -- --side BOTH --regime-replay --no-grid --dump-trades --out docs/research/2026-10-15-regime-replay
```

`--no-grid` limits it to the 94 named experiments (the full 600-cell grid adds nothing to this
question). Comparison table: `regime-compare.txt`. Explorer:
<http://localhost:8899/2026-10-15-regime-replay/explorer.html>

## Baseline — current live settings, with and without the layer

| side | scope | trades off → on | net ₹ off → on | change ₹ | PF off → on | days off → on |
|---|---|---|---|--:|---|---|
| CALL | ALL | 35 → 14 | +35,909 → **−679** | **−36,588** | 2.39 → 0.94 | 15 → 9 |
| CALL | BANKNIFTY | 12 → 2 | +32,849 → −2,982 | −35,832 | 4.64 → 0.00 | 8 → 1 |
| CALL | NIFTY | 5 → 1 | +5,910 → +1 | −5,909 | 3.64 → — | 5 → 1 |
| CALL | SENSEX | 29 → 12 | +6,749 → +2,304 | −4,446 | 1.36 → 1.26 | 10 → 8 |
| PUT | ALL | 146 → 147 | +20,805 → +24,843 | +4,038 | 1.16 → 1.18 | 44 → 43 |
| PUT | BANKNIFTY | 109 → 109 | +34,462 → +26,419 | −8,043 | 1.34 → 1.25 | 39 → 37 |
| PUT | NIFTY | 15 → 12 | −1,721 → −6,628 | −4,907 | 0.92 → 0.63 | 12 → 9 |
| PUT | SENSEX | 58 → 56 | +28,739 → +21,872 | −6,867 | 1.86 → 1.63 | 15 → 14 |

The PUT `ALL` row improves while all three PUT symbols get worse: with one position shared across
symbols, suppressing one entry frees the slot for another, so `ALL` is not the sum of the symbols.
Read the per-symbol rows for the layer's real effect.

## Faithfulness check

The replayed layer reproduces today exactly: with the layer **on**, baseline takes **0 trades on
2026-10-15**, matching the live system (0 directional signals, `SignalGenerated` false on all 1,035
symbol-minutes). With it off, baseline takes 4 winners.

A wider parity check is **not available**: `--parity` crashes before printing
(`ParityCheck.Compare`, `src/Trading.Backtest/Data/ParityCheck.cs:44` — `ToDictionary` on the
decision minutes throws `An item with the same key has already been added. Key: 30-07-2026
15:13:00`, so it assumes one point per symbol-minute and the series holds a duplicate). Until that
is fixed, the rebuilt regime is validated on one day only, and every figure above should be read
as "the layer as this replay rebuilds it", not "the layer exactly as it ran live".

## Not a recommendation

This measures one thing: the layer is expensive on CALL in this replay. It does not establish that
switching it off would be safe — the same layer is what keeps the system out of conditions no one
has measured here (gap days, event days, the first minutes of a session), costs and slippage are
not modelled, and 30 of 35 CALL baseline trades use the modelled premium path. Any change to it
needs its own proposal and paper test.
