# Backtest Condition Discovery + Execution Parity Audit

## Objective

Identify robust CALL and PUT entry conditions using historical data, then independently verify whether the historical simulation reproduces the real trading system's execution behavior.

The system MUST keep condition discovery and actual execution validation separate.

---

# STAGE 1 — CONDITION DISCOVERY

## 1. Historical Data Only

Use exclusively:
- Historical indicator snapshots.
- Historical candles.
- Historical underlying prices.
- Historical option prices, where available.
- Features calculated at the simulated entry timestamp.
- Simulated target, stop-loss, MFE, MAE and P&L.

**STRICT RULE:** Actual closed-trade P&L must never rank, select, reject or optimize historical conditions.

## 2. Independent CALL and PUT Discovery

Run separate condition searches for CALL and PUT.

Test available combinations of:
- RSI5m bands.
- PCR bands.
- Gap Up / Gap Down / Normal.
- EMA1m alignment.
- EMA5m alignment.
- ATR percentage.
- Trend.
- BlockedDelta, only when sufficiently populated.

Test combinations with 1, 2, 3 and 4 filters.

Do not assume that a successful PUT condition will work for CALL.

## 3. Chronological Validation

Split historical data chronologically:

- Training: 60%.
- Validation: 20%.
- Holdout: 20%.

Never randomly shuffle trading dates.

Apply identical simulation assumptions, costs, slippage, position limits and exit rules to every candidate.

## 4. Candidate Evaluation

Report for every candidate:

- Number of trades.
- Number of trading days.
- Gross profit and gross loss.
- Net simulated P&L.
- Profit factor.
- Win rate.
- Expectancy per trade.
- Maximum drawdown.
- Training, validation and holdout performance.
- Performance by individual trading week.

Suggested minimum research requirements:

| Metric | Requirement |
|---|---:|
| Training trades | 100 |
| Validation net P&L | Positive |
| Holdout trades | 30 |
| Holdout net P&L | Positive |
| Holdout profit factor | Above 1.20 |
| Trading-day coverage | At least 5 holdout days |

These are research filters, not guarantees of future profitability.

## 5. Robustness Checks

For promising conditions:

- Test nearby RSI boundaries.
- Test nearby PCR boundaries.
- Compare Gap Up, Gap Down and Normal separately.
- Check whether one unusually profitable day dominates results.
- Check performance across different weeks.
- Compare simple conditions against complex combinations.
- Penalize excessive filter complexity.
- Report how many combinations were tested.

Do not select a condition just because it has the highest historical P&L.

## 6. Discovery Classification

Classify candidates as:

- FAILED: Does not meet validation requirements.
- PROMISING: Positive results but insufficient sample size or stability.
- VALIDATED HISTORICALLY: Meets all defined historical requirements.
- INSUFFICIENT DATA: Not enough reliable observations.

A historically validated condition is still not automatically approved for live trading.

---

# STAGE 2 — EXECUTION PARITY AUDIT

## Objective

Determine why actual trading results differ from historical simulated results.

This stage diagnoses simulation accuracy. It MUST NOT influence historical condition rankings.

## 1. Match Actual Signals

Use actual trade logs only for execution comparison.

Match records using:

1. SignalId, where available.
2. Symbol.
3. Direction.
4. Entry timestamp.
5. Entry price, within a defined tolerance.

Create a matched-signal report.

Classify records as:

- MATCHED.
- ACTUAL_ONLY.
- SIMULATION_ONLY.
- ENTRY_MISMATCH.
- DATA_MISSING.

Never silently discard unmatched records.

## 2. Reconstruct Real Execution

Where historical data is available, reproduce the actual strategy's execution:

- Actual signal timestamp.
- Actual option contract and strike.
- Actual option entry premium.
- Quantity and lot size.
- Initial stop-loss.
- Target levels.
- Partial exits.
- Protected stop-loss.
- Trailing stop-loss.
- Final exit.
- Brokerage and transaction costs.
- Slippage.

Use the same exit priority and execution sequence as the real strategy.

If required option-price or candle data is unavailable, mark the simulation as incomplete. Do not invent prices or exits.

## 3. Compare Actual and Simulated Results

For matched signals, report:

| Field | Actual | Simulated |
|---|---|---|
| Entry timestamp | | |
| Entry premium | | |
| Exit timestamp | | |
| Exit premium | | |
| Quantity | | |
| Exit reason | | |
| Net P&L | | |

Calculate:

- Matched signal count.
- Matching percentage.
- Actual net P&L.
- Simulated net P&L.
- P&L difference.
- Entry-price difference.
- Exit-price difference.
- Exit-reason mismatch percentage.
- Partial-exit mismatch percentage.

Investigate discrepancies by category:

- Different entry timing.
- Missing or incorrect option prices.
- Incorrect candle alignment.
- Stop-loss execution mismatch.
- Target execution mismatch.
- Partial-exit mismatch.
- Trailing-stop mismatch.
- Brokerage or slippage mismatch.

## 4. Execution Accuracy Classification

Classify the simulation:

- HIGH PARITY: Entry and exit behavior closely match.
- PARTIAL PARITY: Some execution differences remain.
- LOW PARITY: Material differences exist.
- INSUFFICIENT DATA: Reliable comparison is impossible.

Define numerical tolerances before running the audit. Do not change tolerances after seeing results to make a candidate pass.

---

# STAGE 3 — FINAL RESEARCH REPORT

Generate separate sections for CALL and PUT.

## A. Condition Discovery

Include:

- Best historical candidates.
- Baseline comparison.
- Training, validation and holdout metrics.
- Sample size and trading-day coverage.
- Failed candidates.
- Robustness results.
- Limitations and multiple-testing risk.

## B. Execution Parity

Include:

- Matched and unmatched signal counts.
- Actual versus simulated P&L.
- Entry and exit differences.
- Exit-reason differences.
- Data-quality issues.
- Execution parity classification.

## C. Final Decision

Use these classifications:

| Discovery | Execution parity | Decision |
|---|---|---|
| Failed | Any | Reject candidate |
| Promising | High | Continue paper testing |
| Historically validated | High | Eligible for forward paper testing |
| Historically validated | Low | Fix simulation first |
| Any | Insufficient data | Collect more data |

## STRICT SAFETY RULES

1. Never use actual closed-trade P&L to rank historical conditions.
2. Never mix actual P&L with simulated P&L in candidate rankings.
3. Never use the execution parity audit to optimize condition boundaries.
4. Never automatically modify live trading configuration.
5. Never claim a condition is profitable based only on a small sample.
6. Never claim historical validation guarantees future profitability.
7. Keep CALL and PUT research independent.
8. Report missing data and unmatched signals explicitly.
9. Preserve existing research outputs and assumptions.
10. Require explicit user approval before any live strategy configuration change.

## FINAL OUTPUT

Always show:

1. Best CALL conditions.
2. Best PUT conditions.
3. Baseline versus candidate results.
4. Training, validation and holdout performance.
5. Execution parity findings.
6. Failed and promising conditions.
7. Data limitations.
8. Recommended next research step.

The final conclusion must clearly distinguish:

- What performed well historically.
- What reproduces actual execution.
- What is ready for further paper testing.
- What remains unproven.

Never describe a condition as a proven live-trading edge.