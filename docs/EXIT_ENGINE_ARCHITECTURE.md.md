# EXIT_ENGINE_ARCHITECTURE.md

# CRA Exit Engine Architecture

## ROLE

The Exit Engine is responsible for managing every active position until it is completely closed.

Its purpose is to maximize profitable exits while minimizing losses.

The Exit Engine continuously evaluates market conditions and position health.

It never ignores risk.

---

# PRIMARY OBJECTIVE

Protect Capital

↓

Reduce Losses

↓

Maximize Profits

↓

Lock In Gains

↓

Exit Positions Intelligently

---

# EXIT PIPELINE

Position Open

↓

Position Validation

↓

Market Snapshot

↓

Risk Evaluation

↓

Stop Loss Check

↓

Target Check

↓

Trailing Stop

↓

Time Exit

↓

Expiry Exit

↓

Emergency Exit

↓

Position Closed

↓

Trade Journal

↓

Learning Engine

---

# INPUT DATA

Use

Current Position

Entry Price

Current Price

Stop Loss

Targets

ATR

EMA

VWAP

RSI

PCR

OI

Greeks

Volume

Market Regime

Risk Score

AI Recommendation

Broker Status

---

# EXIT TYPES

Support

Initial Stop Loss

Target 1

Target 2

Target 3

Trailing Stop

Break Even Stop

Time Exit

Expiry Exit

HeroZero Exit

Risk Exit

AI Suggested Exit

Emergency Exit

Manual Exit

---

# STOP LOSS MANAGEMENT

Validate

Stop Loss Exists

Stop Loss Distance

Risk Reward Ratio

ATR Alignment

Market Volatility

Reject invalid stop loss configurations.

---

# TARGET MANAGEMENT

Support multiple targets

Target 1

Partial Exit

Target 2

Additional Profit Booking

Target 3

Final Exit

Allow configurable quantity percentages.

---

# TRAILING STOP

Support

Fixed Points

ATR Based

EMA Based

Swing High / Low

VWAP Based

AI Recommended

Trailing stop must never reduce locked profit.

---

# BREAK EVEN LOGIC

Move stop loss to break even when

Minimum profit threshold reached

Risk becomes zero

Market conditions remain favorable

Never move stop prematurely.

---

# TIME BASED EXIT

Exit positions when

Maximum Holding Time Exceeded

End Of Trading Session

Configured Time Reached

Strategy Time Limit Reached

---

# MARKET REGIME EXIT

Adjust exits for

Bull Market

Bear Market

Sideways

High Volatility

Low Volatility

Expiry

News

Gap Up

Gap Down

Exit behaviour should adapt to the market regime.

---

# HEROZERO EXIT

Evaluate

HeroZero Score

Confidence

Momentum Loss

Trend Reversal

OI Change

PCR Shift

Generate exit recommendation.

---

# RISK EXIT

Immediately exit when

Maximum Risk Exceeded

Daily Loss Limit Reached

Portfolio Risk Critical

Broker Error

Market Halt

Infrastructure Failure

---

# PARTIAL EXIT

Support

25%

50%

75%

100%

Partial exits should be configurable by strategy.

---

# EXIT PRIORITY

Priority

Emergency Exit

↓

Risk Exit

↓

Stop Loss

↓

Target

↓

Trailing Stop

↓

Time Exit

↓

AI Suggested Exit

Never ignore higher-priority exits.

---

# EXIT VALIDATION

Before closing a position verify

Broker Connected

Order Accepted

Position Exists

Quantity Valid

Price Valid

Execution Confirmed

---

# POSITION LIFECYCLE

Entry

↓

Open

↓

Monitor

↓

Partial Exit

↓

Trailing Stop

↓

Final Exit

↓

Trade Journal

↓

Analytics

↓

Learning Engine

---

# AI INTEGRATION

Receive recommendations from

Market Intelligence

Risk Engine

Self Learning AI

AI Decision Engine

Performance Analytics

AI recommendations are advisory.

Exit rules remain deterministic.

---

# LOGGING

Record

Exit Type

Exit Time

Exit Price

PnL

Reason

Market Regime

Risk Score

AI Recommendation

Execution Time

---

# PERFORMANCE TARGETS

Exit Evaluation

<500 ms

Risk Exit

<250 ms

Stop Loss Processing

<250 ms

Broker Confirmation

<2 Seconds

---

# OUTPUT FILES

Generate

ExitEngineReport.md

ExitStatistics.md

ExitReasons.csv

PartialExits.csv

TrailingStopHistory.csv

ExitPerformance.md

---

# SAFETY

Never

Remove Stop Loss

Ignore Risk

Ignore Broker Errors

Exit Without Validation

Generate Duplicate Exit Orders

Use Stale Market Data

---

# SUCCESS CRITERIA

The Exit Engine succeeds when

Losses are limited.

Profits are protected.

Trailing stops lock in gains.

Emergency exits function correctly.

Every exit is explainable, validated and recorded.