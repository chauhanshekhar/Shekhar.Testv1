# Proposal — cap entry distance from EMA20 (BANKNIFTY PUT)

**Status:** proposal only. No code written, no setting changed, nothing deployed.
**Date:** 2026-10-15
**Evidence:** `docs/research/2026-10-15-stretch-sweep/`, `docs/research/2026-10-15-stretch-banknifty-put/`,
`docs/research/2026-10-15-stretch-costed/`

---

## Problem

The production PUT chain is **loss-making once costs are applied.** At the user's figures
(Rs20/order, 1 point slippage per side), over the full stored history:

| Scope | Trades | Gross | **Net after costs** |
|---|---:|---:|---:|
| PUT all symbols | 146 | +20,805 | **-30,455** |
| PUT BANKNIFTY | 109 | +34,462 | **-4,038** |

Costs run ~Rs350/trade and total Rs51,260 — 2.5x the gross profit. Every earlier research
conclusion in this repo was drawn on gross numbers, so changes that *add* trades were
systematically overrated.

## Proposed rule

Refuse a PUT entry on BANKNIFTY when price has already run more than **0.65 ATR below the
1-minute EMA20** at the entry minute:

```
stretch = (Ema20 - Ltp) / Atr          // PUT; mirror for CALL
block if stretch > StretchMaxAtr
```

ATR is the 1-minute ATR in index points (the same series `AtrRiskService` uses for the stop), so
the threshold is volatility-normalised rather than a fixed point distance.

## Evidence

Walk-forward, **net after costs**, BANKNIFTY PUT. Training and validation decide; the test column
was never used to rank.

| Config | n | Net after costs | PF | Val PF | Test PF | Validated |
|---|---:|---:|---:|---:|---:|:--:|
| Baseline | 109 | **-4,038** | 1.34 | 0.91 | 3.46 | NO |
| Max 0.60 | 66 | +14,956 | 1.72 | 2.50 | 3.74 | YES |
| **Max 0.65** | 70 | **+29,435** | 2.07 | 2.75 | 3.74 | YES |
| Max 0.75 | 77 | +21,754 | 1.81 | 2.75 | 2.49 | YES |
| Max 0.85 | 78 | +26,378 | 1.89 | 2.81 | 2.85 | YES |
| Max 0.95 | 79 | +28,358 | 1.97 | 2.79 | 2.85 | YES |
| Max 1.10 | 87 | +9,755 | 1.58 | 1.75 | 2.24 | YES |
| Min 1.00 *(contrast)* | 97 | **-20,576** | 1.14 | 0.97 | 1.44 | NO |

Four things make this more than a tuned number:

1. **Every cap from 0.60 to 1.10 validates** and stays positive after costs. A plateau, not a spike.
2. **The contrast arm fails.** Taking *only* stretched entries is the worst configuration tested —
   the result a real effect predicts, and the test the live-trade analysis could not run.
3. **It reduces trade count** (109 -> 70), so costs move in its favour. It is the only change tested
   today that improves gross *and* net.
4. **Max drawdown roughly halves**, 23,653 -> 9,503 (gross).

It also agrees with earlier work rather than overturning it: `docs/research/2026-10-05-ema20-distance/`
rejected a *minimum* distance because near-EMA20 entries are better, and found BANKNIFTY PUT's best
bucket at 0.10-0.25 ATR (PF 2.04). A maximum cap enforces the same preference from the other side.

## What the evidence does NOT support

- **Any other symbol or side.** CALL has 5 training and 3 validation trades (untestable); NIFTY has
  8 trades total; SENSEX has 2 validation trades. This is BANKNIFTY PUT only.
- **"Late entries lose money."** The cap is not a subset filter. With one position at a time,
  blocking an entry re-sequences the whole session: baseline and Max0.85 share only 21 trades, the
  88 dropped were collectively *profitable* (+29,921), and the gain comes from 57 entries baseline
  never took. The mechanism is opportunity cost of the position slot, not bad trades being removed.
- **A specific optimum.** 0.65 is the best single number, but 0.85 and 0.95 are statistically
  indistinguishable. Pick for robustness, not for the maximum.

## Implementation sketch (not written)

No `StrategySettings` key exists for this — the rule lives only in `Trading.Backtest`. It needs a
new gate, following `EarlyTrendGate`:

- **`StretchGate : IPostSignalGate`** in `Trading.Worker/SignalGates/` — post-signal, because the
  direction only exists once the signal is built. Registered in the `PostGates` array in
  `SignalWorker.cs` (order matters).
- Settings, both read through `StrategySettingsService`:
  - `EnableStretchGate` — 0/1, **default 0 (off)**.
  - `StretchMaxAtr` — decimal, default 0.65, set as a **BANKNIFTY-only row** so no other symbol is
    affected.
- Fail open: if ATR or EMA20 is unavailable (0), let the signal through, as the other entry gates do.
- HeroZero exempt, consistent with the existing gates.
- xUnit tests in `Trading.Tests` next to `CallEntryGateTests` / `BreakevenArmTests`: blocks beyond
  the threshold, allows within it, allows when ATR is 0, and is inert when the flag is off.
- Kill switch: `UPDATE StrategySettings SET SettingValue = 0 WHERE SettingKey = 'EnableStretchGate'`,
  shipped as a rollback script per `.claude/rules/02-approval-gate.md`.

## Risk

| Risk | Assessment |
|---|---|
| Cuts trade count ~36% | Intended, and the source of the cost saving. But fewer trades = slower feedback. |
| Fitted to BANKNIFTY PUT | Real. Scope the setting to BANKNIFTY and leave every other symbol untouched. |
| Mechanism is re-sequencing | The benefit depends on *other* setups existing later in the session. If entry frequency changes, the effect may not hold. |
| Sample size | 109 baseline trades across ~68 sessions. Validated, but not large. |
| Premium path | Real `OptionMinuteHistory` only from 2026-10-07; earlier trades use index move x average delta. |
| Slippage assumption | 1 point per side, uniform. Real slippage varies with spread and urgency. |

## Validation before any deploy

1. `dotnet build TradingPlatform.slnx` and `dotnet test src/Trading.Tests` pass (906 tests green as
   of this proposal).
2. Re-run `--stretch-sweep` with costs and confirm the plateau still holds on data including the
   newest sessions.
3. Enable on **paper only**, BANKNIFTY PUT, for at least 10 sessions; compare realised trades
   against the base rate before considering live.
4. Keep the flag off by default so a deploy alone changes nothing.

## Open question — the cap hurts on gap-down sessions

Added 2026-10-15 after `docs/research/2026-10-15-stretch-by-opening/`, which splits each session by
how it opened and runs the cap with and without, costed. BANKNIFTY PUT, net after costs:

| Opening | No cap | Max 0.85 | Effect of the cap |
|---|---:|---:|---|
| GapUp | -18,323 (val PF 0.59, NO) | **+4,790** (val PF 2.02, YES) | rescues it |
| **GapDown** | **+14,714** (val PF 3.80, YES) | +6,771 | **costs -7,943** |
| Normal | -430 (val PF 0.25, NO) | **+14,817** (val PF 1.57, YES) | rescues it |

On a gap-down open price starts far below EMA20, so a PUT cap refuses the continuation entries that
are supposed to work. Gap-down is the only opening type where the unconstrained chain is already
profitable and validates on its own, and the cap cuts it by more than half (30 trades -> 22).

This also shows the aggregate result was masking structure: without the cap, GapUp and Normal are
both loss-making after costs. The cap rescues two regimes and damages one.

**A flat cap is therefore the wrong rule.** The obvious fix is "cap except on gap-down sessions",
which on this data would be worth roughly +34,300 against +26,378 for the flat cap. That number is
NOT evidence: the exclusion was chosen from the same data that produced the finding, which is the
overfitting the walk-forward split exists to prevent. A conditional rule needs its own validated run
with the exclusion fixed in advance.

Also thin: GapDown has 6-10 validation trades, and the 8.18 validation PF is 7 trades — ignore it.
Gap classification needs `DayOpen`/`YesterdayClose`, absent for ~20% of live trades.

## Day-type split — the cap is a range-day filter, not a late-entry filter

Added 2026-10-15 from `docs/research/2026-10-15-day-types/` (new `SessionClassifier`, `--day-types`).
Every session is labelled by how it opened AND what it did next. PUT trades, **net of each
configuration's measured per-symbol cost**:

| Day type | Baseline n / net / avg | Max 0.65 avg | Max 0.85 avg | Effect of the cap |
|---|---|---:|---:|---|
| **GapDown/Continuation** | 27 / **+18,730** / **+694** | +392 | +328 | **HURTS** |
| Normal/TrendDown | 54 / +15,130 / +280 | +539 | +406 | helps |
| **Normal/Range** | 34 / +9,258 / +272 | **+874** | +861 | **helps most** |
| GapDown/Reversal | 8 / -1,877 / -235 | -374 | -244 | both poor |
| GapUp/Reversal | 14 / -5,457 / -390 | +2 | +286 | helps |
| Normal/TrendUp | 14 / -7,923 / -566 | +467 | +268 | helps |
| GapDown/Range | 17 / -8,669 / -510 | +129 | +196 | helps |
| **GapUp/Range** | 14 / **-21,492** / **-1,535** | +129 | -186 | **rescues** |
| **Total** | **182 / -2,299** | **+50,339** (n=118) | **+47,613** (n=127) | |

Two conclusions:

1. **The harm is narrower than "gap-down".** Only **GapDown/Continuation** is damaged - gap-down
   sessions that keep falling, the ideal PUT regime, where price runs far from EMA20 and the cap
   refuses the continuation. GapDown/Range (-510 -> +196) and GapDown/Reversal are fine or improved.
2. **The cap earns its money on chop.** Its largest gains are Normal/Range (+272 -> +874 per trade)
   and GapUp/Range (-1,535 -> +129). It is a **range-day filter**, which is also why the original
   "late entries lose money" framing never held up: the 88 trades it drops are collectively
   profitable, and the gain comes from entries the baseline never took.

Baseline is **-2,299 after costs** across these 182 trades - break-even at best.

## The day-type buckets are censored by the RSI band — read the totals accordingly

Checked 2026-10-15 against all 20,682 snapshot minutes (`docs/research/2026-10-15-day-types/`),
after the question "on gap days RSI and PCR are high, so the system never trades — is the sample
real?". It is a fair challenge and the answer is mixed.

**The 5m RSI band is what selects the sample; PCR is not.** Share of minutes inside the live PUT
band (RSI5 28-38), and inside `PutPcrMinimum` 0.70:

| Day type | Minutes | RSI5 median | In 28-38 | PCR >= 0.70 |
|---|---:|---:|---:|---:|
| Normal/TrendDown | 5,340 | 43.0 | 27% | 98% |
| GapUp/Reversal | 671 | 42.0 | 25% | 94% |
| GapDown/Continuation | 1,645 | 44.6 | 22% | 86% |
| GapDown/Range | 1,017 | 42.1 | 22% | 99% |
| Normal/Range | 2,991 | 49.9 | 17% | 90% |
| GapUp/Range | 2,038 | 51.2 | 7% | 98% |
| Normal/TrendUp | 5,588 | 60.2 | 5% | 99% |
| **GapUp/Continuation** | 1,379 | 58.5 | **3%** | 100% |

PCR passes 86-100% of minutes everywhere — it binds on nothing. RSI does all the selecting.

**Consequence: two buckets are effectively absent.** GapUp/Continuation produced **zero** PUT trades
across 9 sessions, and Normal/TrendUp 14 trades across 29. That is structural, not sampling noise:
only 3% and 5% of their minutes are in band. **Any "PUT across all day types" total is really
"across the day types the RSI band admits"**, and should not be read as covering the space.

**But the challenge does not overturn the gap-down finding.** Entry frequency per session, PUT
baseline:

| Day type | Sessions | Trades | Trades/session |
|---|---:|---:|---:|
| GapDown/Continuation | 12 | 27 | **2.25** (highest) |
| GapDown/Range | 8 | 17 | 2.12 |
| Normal/TrendDown | 39 | 54 | 1.38 |
| Normal/Range | 51 | 34 | 0.67 |
| GapUp/Continuation | 9 | 0 | 0.00 |

Gap-**down** days are the system's most active for PUT, not its least. So "+694/trade baseline ->
+328 capped" on GapDown/Continuation rests on the best-sampled gap bucket. The censoring is on the
gap-**up** and rising-day side, where a PUT should not trade anyway.

It does reframe what the cap is for: the RSI band already excludes rising days, so the cap's gains
come from the chop RSI still admits — Normal/Range (17% of minutes in band) and GapUp/Range (7%).
The cap cleans up after RSI on indecisive days.

**`IndicatorSnapshotOutcomes` was deliberately not used.** Its `IndicatorSnapshotId` values were
reseeded and resolve to the wrong snapshot row (a prior check found 0 of 155,373 joined rows where
symbol and price both agreed). The join fails silently rather than loudly, so it is excluded from
this and every other analysis here.

## Why "cap except on gap-down continuation" cannot be implemented

Continuation / Reversal / Range is only knowable at the close. `SessionClassifier` lives in
`Trading.Backtest/Reporting/`, deliberately unreachable from `EntryRules`, because a gate reading it
would be trading on the future. **The day-type split above is reporting, not a rule.**

## The entry-time-observable version (sketch, untested)

What the cap is really trying to avoid is *entering a stretched position on a day that is chopping*.
That is approximable from values already known at the entry minute, all present on `MarketPoint`
and verified to be running values (computed from bars strictly before the current one):

- `SessionOpen` - the session's first 1m open.
- `DayHigh` / `DayLow` - the session's extremes **so far** (`MarketSeriesBuilder` accumulates these
  over `q < j`, i.e. up to but excluding the current bar).
- `Ltp`, `Ema20`, `Atr`, `OpenGapPct`.

Define, at the entry minute:

```
travelled    = |Ltp - SessionOpen|
rangeSoFar   = DayHigh - DayLow
efficiency   = rangeSoFar > 0 ? travelled / rangeSoFar : 1     // 0 = round-tripping, 1 = one-way
```

`efficiency` is the live analogue of the `DecisiveFraction` test the classifier applies at the
close. A session that is trending has travelled most of its range in one direction; a chopping
session has not.

**Proposed rule:** apply the stretch cap **only when the session so far looks range-bound**:

```
if (efficiency < EfficiencyMin)            // e.g. 0.35, the classifier's own threshold
    apply MaxStretchAtr as proposed
else
    no cap - the session is trending, let the continuation entry through
```

Optionally also waive the cap when `OpenGapPct` agrees with the trade direction (gap down for a
PUT) **and** efficiency is high - the live approximation of GapDown/Continuation.

This is a hypothesis, not a result. It must be run through `--stretch-sweep`-style walk-forward
with `EfficiencyMin` fixed in advance, costed, before it is worth anything. Note the obvious risk:
`efficiency` early in a session is noisy (a small `rangeSoFar` makes the ratio unstable), so it
likely needs a minimum bar count or a minimum `rangeSoFar` in ATRs before the test applies.

## Recommendation

**Do not implement the flat cap.** The day-type split shows it damages GapDown/Continuation - the
best PUT regime there is (+694/trade before the cap, +328 after) - while earning its keep on range
days. A flat cap pays for a chop filter with the continuation trades that fund the strategy.

Next step is a validated run of the conditional rule — cap on GapUp and Normal sessions, no cap on
GapDown — with the exclusion fixed before the run rather than read off these results. If that
validates on its own training/validation split and survives costs, it becomes the proposal. If it
does not, the honest outcome is that the effect is regime-specific and too thin to ship.

If something must ship sooner, the defensible subset is the cap applied **only to GapUp and Normal
sessions** on BANKNIFTY PUT, behind `EnableStretchGate = 0`, paper-tested for at least 10 sessions.
That still carries the caveat above.

**Do not implement without explicit approval** (`.claude/rules/02-approval-gate.md`).
