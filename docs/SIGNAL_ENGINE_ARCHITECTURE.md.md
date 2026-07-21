# SIGNAL_ENGINE_ARCHITECTURE.md

# CRA Signal Engine Architecture

## ROLE

The Signal Engine is the heart of the CRA Trading Platform.

Its responsibility is to generate high-quality trading opportunities using validated market data, technical indicators, option chain analysis, AI recommendations, and risk management.

The Signal Engine never bypasses validation.

Every generated signal must be explainable.

---

# PRIMARY OBJECTIVE

Generate

High Probability Signals

Reduce

False Signals

Respect

Market Regime

Risk Limits

AI Recommendations

Capital Preservation

---

# SIGNAL PIPELINE

Live Tick

↓

Tick Validation

↓

Candle Aggregation

↓

Indicator Calculation

↓

Market Snapshot

↓

Market Regime Analysis

↓

Strategy Selection

↓

Entry Filters

↓

Risk Validation

↓

Confidence Scoring

↓

Duplicate Check

↓

Cooldown Check

↓

Signal Generation

↓

Trade Monitoring

↓

Exit Processing

---

# INPUT DATA

Use

Market Snapshot

EMA

ATR

VWAP

RSI

MACD

ADX

Option Chain

PCR

OI

Greeks

Volume

Market Regime

Strategy Ranking

Risk Score

Portfolio Allocation

AI Decision

---

# ENTRY VALIDATION

Every signal must validate

Trend Direction

ATR Threshold

EMA Alignment

RSI Range

VWAP Confirmation

PCR Filter

OI Confirmation

Volume Confirmation

Liquidity

Spread

Market Regime

Risk Score

AI Recommendation

---

# STRATEGY EXECUTION

Strategies execute in order

Tier S

↓

Tier A

↓

Tier B

↓

Experimental

Disabled strategies are ignored.

---

# CONFIDENCE SCORE

Calculate

Trend Score

Momentum Score

Volume Score

ATR Score

VWAP Score

PCR Score

OI Score

Delta Score

Liquidity Score

Market Regime Score

AI Confidence

Overall Confidence

0–100

Reject signals below configured confidence.

---

# DUPLICATE PREVENTION

Prevent

Duplicate Signal

Repeated Entry

Multiple Entries Same Direction

Rapid Signal Generation

Repeated Alerts

---

# COOLDOWN RULES

Support

Symbol Cooldown

Strategy Cooldown

Direction Cooldown

Loss Cooldown

Time Cooldown

Cooldown values must be configurable.

---

# ENTRY RULES

Before generating a signal

Confirm

No Existing Position

Strategy Enabled

Risk Approved

Market Open

Broker Connected

Workers Healthy

Data Fresh

---

# EXIT ENGINE

Support

Stop Loss

Target 1

Target 2

Trailing Stop

Time Exit

HeroZero Exit

Expiry Exit

Risk Exit

Emergency Exit

---

# TRADE LIFECYCLE

Signal Generated

↓

Entry

↓

Position Active

↓

Position Monitoring

↓

Partial Exit

↓

Trailing Stop

↓

Final Exit

↓

Trade Journal

↓

Performance Analytics

↓

Learning Engine

---

# AI INTEGRATION

Receive

Market Intelligence

Risk Recommendation

Parameter Recommendation

Strategy Recommendation

Portfolio Recommendation

Decision Recommendation

AI never places trades.

The Signal Engine remains the execution decision point.

---

# RISK VALIDATION

Before every signal verify

Daily Loss Limit

Drawdown

Exposure

Margin

Position Size

Risk Reward Ratio

Liquidity

Broker Status

Reject unsafe signals.

---

# MARKET REGIME INTEGRATION

Adjust behaviour for

Bull

Bear

Sideways

High Volatility

Low Volatility

Gap Up

Gap Down

Expiry

News

Holiday

---

# SIGNAL PRIORITY

Rank

Excellent

Good

Average

Poor

Rejected

Only Excellent and Good should normally generate trading opportunities.

---

# SIGNAL OUTPUT

Each signal must contain

Signal ID

Timestamp

Strategy

Symbol

Direction

Entry

Stop Loss

Targets

Confidence

Risk Score

Market Regime

Reason

Supporting Indicators

AI Summary

---

# LOGGING

Record

Generated Signals

Rejected Signals

Reason For Rejection

Confidence Breakdown

Risk Breakdown

Execution Time

Strategy Used

Market Regime

---

# PERFORMANCE TARGETS

Signal Generation

<2 Seconds

Risk Validation

<1 Second

Confidence Calculation

<500 ms

Signal Broadcast

<500 ms

---

# OUTPUT FILES

Generate

SignalEngineReport.md

SignalStatistics.md

RejectedSignals.md

SignalConfidence.csv

StrategySignals.csv

SignalLatency.csv

---

# SAFETY

Never

Generate Signals Without Validation

Ignore Risk

Ignore Market Regime

Ignore AI Recommendations

Generate Duplicate Signals

Use Stale Market Data

Override User Configuration

---

# SUCCESS CRITERIA

The Signal Engine succeeds when

Signals are accurate.

False signals are minimized.

Risk remains controlled.

Recommendations are explainable.

Execution remains fast and reliable.

Capital protection always has priority over signal frequency.