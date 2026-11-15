# Proposal — widen the BANKNIFTY PUT 5m RSI band from 28-38 to 32-45

**Status: WITHDRAWN 2026-10-16. Do not run the script.**

The evidence below was measured on a **selected** sample - only minutes the live RSI band already
admitted, plus what the widened band let in. It never saw the minutes the band correctly excluded,
so it could not tell whether the band was doing useful work.

Re-measured on the unfiltered base rate (`docs/research/2026-10-16-base-rate/`: every decision
minute, no RSI/PCR/ATR/gap filter, no position limit, 2,714 PUT entries vs 328 under live rules),
net after costs **per trade**:

| RSI filter | GapDown | Normal | GapUp |
|---|---:|---:|---:|
| none | -28 | -48 | -25 |
| **live 28-38** | **+266** | **+141** | -254 |
| **proposed 32-45** | **-155** | **-94** | **-265** |
| <32 only | +50 | +157 | +215 |
| >=45 only | +135 | -85 | +122 |

**The live band is the best filter tested on GapDown and Normal; 32-45 is worse than the live band
on every opening type**, turning two profitable regimes into losing ones. The +35,176 that motivated
this proposal was an artefact of asking a band-selected sample what the band should be.

What survives: on **GapUp** days the live band is negative (-254) while both tails are positive
(<32 = +215, >=45 = +122) - the middle is the worst place to be, the opposite shape from GapDown.
One band cannot serve both openings. That is a direction for new work, not a rule: the cells are
29-311 trades, overlapping positions are not independent, and none of it is walk-forward split.

---

**Original proposal below, retained for the record. Its conclusion is wrong.**

**Status:** proposal only. Nothing executed, nothing deployed.
**Date:** 2026-10-16
**Script:** [`propose-banknifty-put-rsi-band.sql`](./propose-banknifty-put-rsi-band.sql)
**Evidence:** `docs/research/2026-10-15-put-rsi-band/`, `docs/research/2026-10-15-day-types/`

---

## Problem

The production PUT chain loses money once costs are applied (Rs20/order + 1pt slippage per side,
the user's own figures):

| Scope | Trades | Gross | **Net after costs** |
|---|---:|---:|---:|
| PUT all symbols | 146 | +20,805 | **-30,455** |
| PUT BANKNIFTY | 109 | +34,462 | **-4,038** |

The live 5m RSI band for PUT is **28-38**, applied globally.

## Proposed change

Give BANKNIFTY its own band, **32-45**. Two `StrategySettings` rows, both symbol-scoped:

| Key | Global (unchanged) | New BANKNIFTY row |
|---|---:|---:|
| `PutRsiFloor` | 28 | **32** |
| `PutRsiCeiling` | 38 | **45** |

No code change. Both keys already exist and are already read by the live engine
(`SignalEngineService.RsiConfidenceBand`). NIFTY and SENSEX are untouched.

## Evidence

BANKNIFTY PUT over the full stored history (~68 sessions), **net after costs**, walk-forward.
Training and validation decide; the test column was never used to rank.

| Band | n | Win % | Net after costs | PF | Train PF | Val PF | Test PF | Validated |
|---|---:|---:|---:|---:|---:|---:|---:|:--:|
| **28-38 (live)** | 109 | 51% | **-4,038** | 1.34 | 1.01 | 0.91 | 3.46 | NO |
| Floor 32 only (32-38) | 89 | 55% | +10,418 | 1.54 | 1.30 | 0.90 | 3.20 | NO |
| Ceiling 45 only (28-45) | 132 | 55% | +20,969 | 1.63 | 1.54 | 1.15 | 2.16 | YES |
| **32-45** | 114 | **58%** | **+35,176** | **1.86** | **1.92** | **1.43** | 2.04 | **YES** |
| 30-45 | 125 | 58% | +30,387 | 1.75 | 1.75 | 1.17 | 2.20 | YES |
| 25-45 | 142 | 56% | +29,047 | 1.68 | 1.43 | 1.54 | 2.21 | YES |
| 32-50 | 122 | 57% | +27,353 | 1.72 | 1.66 | 1.47 | 1.94 | YES |

A **Rs39,000 swing**, positive in all three windows. Both ends move up, and they combine: the
ceiling alone is worth +20,969, the floor alone +10,418, together +35,176. Neighbouring bands
(30-45, 25-45, 32-50) all validate too, so this is a plateau rather than a tuned point.

### It does not sacrifice a regime

The flat stretch cap (the earlier proposal) bought its aggregate gain by damaging
GapDown/Continuation. This does not. Net after costs by day type:

| Day type | 28-38 (live) | 32-45 | |
|---|---|---|---|
| GapDown/Continuation | 21 / +14,685 / **+699** | 16 / +15,410 / **+963** | better per trade |
| Normal/Range | 26 / +10,452 / +402 | 32 / +14,547 / +455 | better |
| GapDown/Reversal | 6 / -952 / -159 | 6 / +4,838 / +806 | better |
| Normal/TrendDown | 25 / -2,075 / -83 | 22 / +10,760 / +489 | better |
| GapUp/Reversal | 7 / -3,857 / -551 | 7 / +433 / +62 | better |
| Normal/TrendUp | 13 / -8,872 / -682 | 15 / -4,491 / -299 | less bad |
| **GapUp/Range** | 8 / **-15,144** / **-1,893** | 13 / -2,490 / -192 | **much better** |
| **GapDown/Range** | 3 / +1,725 / **+575** | 3 / -3,830 / **-1,277** | **WORSE** |

The user raised the obvious objection: on a gap-down open RSI drops to 20-25, so does a floor of 32
block the best PUT entries? Checked directly - in the 09:15-10:00 window on GapDown/Continuation
days, 42% of minutes sit below RSI 32 (p10 = 23.7). So the floor **does** remove early gap-down
entries - but that bucket's net *rises* (+14,685 -> +15,410) on 5 fewer trades, and its average
improves +699 -> +963. The entries removed were the loss-making ones.

The one bucket that genuinely worsens is **GapDown/Range** (+575 -> -1,277 per trade), on 3 trades.
Noise-level, but recorded rather than hidden.

Most of the gain comes from the **ceiling**, not the floor: GapUp/Range (-1,893 -> -192) and
Normal/TrendDown (-83 -> +489). Raising the ceiling lets the system take PUTs into weakness that has
not yet become oversold, which is where the chop-day losses were.

### PCR is inert

Every PCR variant (0.6 / 0.7 / 0.8 / 0.9) produces **identical** BANKNIFTY numbers. Snapshot data
agrees: `PutPcrMinimum` passes 86-100% of minutes on every day type. The PCR rule binds on nothing
for BANKNIFTY and is not part of this proposal.

## *** The side effect that is not in the backtest ***

`PutRsiFloor` / `PutRsiCeiling` drive **two** things
(`SignalEngineService.RsiConfidenceBand`, ~line 145):

1. the PUT entry band - the intended change; and
2. the RSI term of the **confidence score**, which feeds **lot sizing**:

```
if (rsi <= PutRsiFloor)        score += 15
else if (rsi <= PutRsiCeiling) score += 8
```

After the change, for BANKNIFTY PUT: RSI 28-32 scores +15 instead of +8, and RSI 38-45 scores +8
instead of 0. **Confidence rises, so position size rises.**

**The backtest did not model this.** `ExitModel` uses a fixed lot count (`Math.Max(1, AdaptiveLotMin)`,
currently 4) and nothing in `Trading.Backtest` reads confidence. The +35,176 is at **constant size**.
Live, the same trades would run at larger size, scaling both profit and drawdown by an amount this
evidence does not measure.

This is the main reason to paper-test rather than enable live on the strength of the number.

## What the evidence does NOT support

- **Any other symbol.** SENSEX has **2 validation trades** in every variant (val PF 0.00) and its
  baseline is already +13,539. NIFTY is negative in every configuration tested (15 trades, -11,801
  baseline); nothing fixes it, and that symbol has a different problem.
- **CALL.** Not tested here; the CALL side has 5 training trades and is untestable.
- **A precise optimum.** 32-45 is best, but 30-45, 25-45 and 32-50 all validate. Treat 32-45 as the
  middle of a plateau.
- **Live position sizing.** See the side effect above.

## Risk

| Risk | Assessment |
|---|---|
| Lot sizing rises via confidence | **Unmeasured.** The backtest is fixed-size. Biggest single unknown. |
| Fitted to BANKNIFTY | Scoped to a BANKNIFTY row; other symbols keep the global band. |
| Trade count rises 109 -> 114 | Marginal; costs already included at Rs20 + 1pt. |
| Baseline test PF is 3.46, higher than any variant | The test window happens to suit the current band. Validation, not test, decides - and there the live band is 0.91 (losing). |
| GapDown/Range worsens | Real but 3 trades. |
| Premium path | Real `OptionMinuteHistory` only from 2026-10-07; earlier trades use index move x average delta. |
| Sample | 109 baseline trades over ~68 sessions. Validated, not large. |

## Validation before any deploy

1. `dotnet build TradingPlatform.slnx` and `dotnet test src/Trading.Tests` - **921 tests green** as
   of this proposal (no code change is required, so this is a regression check only).
2. Re-run `--side PUT --no-grid --put-rsi-floors 32 --put-rsi-ceilings 45 --cost-per-order 20
   --slippage-points 1` once newer sessions exist and confirm the plateau holds.
3. Run the script's PRE-FLIGHT and confirm it reports exactly two global rows at 28/38.
4. Apply on **paper** first, BANKNIFTY only, for at least 10 sessions. Compare realised trades
   against the base rate, **and watch the lot sizes** - that is the part the backtest did not model.
5. Settings are cached ~5 minutes; no restart needed.

## Recommendation

Apply **32-45, BANKNIFTY-scoped, on paper**, and review after 10 sessions with lot sizes explicitly
checked. It is the strongest result measured here: a Rs39,000 swing, validated in all three windows,
no regime sacrificed, and - unlike the stretch cap - **no new code, no new gate, two settings rows**.

Prefer this over `docs/propose-banknifty-put-stretch-cap.md`, which is a smaller effect (+26,378)
for a new gate, damages GapDown/Continuation, and whose conditional refinement was tested and
falsified (`docs/research/2026-10-16-efficiency-sweep/`).

**Do not run the script without explicit approval** (`.claude/rules/02-approval-gate.md`).
