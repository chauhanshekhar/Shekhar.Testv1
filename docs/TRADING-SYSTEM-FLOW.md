# CRA Trading Platform — End-to-End Flow

Derived from reading the code (Worker, SignalEngine, Exits, BrokerOrderSync). Items marked
**(not read)** were not opened and are described from the README/CLAUDE.md only.

## 1. Big picture

```
                      ┌─────────────────────────── SQL Server 2022 ───────────────────────────┐
                      │ Ticks · Candles · OptionChain · IndicatorSnapshots · Signals · Orders │
                      │ Positions · TradeJournal · AppErrors · StrategySettings · SystemSettings│
                      └───────────▲───────────────────────────────────────────────▲───────────┘
                                  │                                               │
 Groww API ──► DATA WORKERS ──────┘        SIGNAL PIPELINE ─► ORDER ─► SYNC ─► EXIT PIPELINE
                                                                                  │
 Trading.API (REST + SignalR) ◄───────────────── reads DB, pushes live signals ───┘
 Telegram / WhatsApp ◄──────────────────────────── alerts (critical order/stop events)
```

Single process `Trading.Worker` hosts all background services; `Trading.API` is a separate
container (port 8080). Both run via docker-compose with SQL Server.

## 2. Startup (`Worker/Program.cs`)

1. `EnvLoader.Load()` → `.env`
2. Register logging, DB, broker (Groww), SignalEngine, workers.
3. Before `host.Run()`: load `IInstrumentCache`, load `StrategySettings` cache, and
   `InstrumentStrategyRegistry.ValidateRegistrations` (fail fast if two strategies claim one symbol).
4. Hosted services start (registration order, `WorkerExtensions.cs`):

| # | Worker | Job |
|---|---|---|
| 1 | GrowwTokenRefreshWorker | Daily access token (must precede any broker call) |
| 2 | HolidayCalendarWorker | Holidays before open |
| 3 | HistoricalDataWorker | 1m + 5m candles from Groww |
| 4 | MarketFeedWorker | Live index ticks (~1 s loop) |
| 5 | EmaWorker | EMA20/EMA50 from candles |
| 6 | ExpiryRollWorker | Rolls instrument expiry (only if `EnableExpiryAutoRoll`) |
| 7 | OptionChainWorker | Chain snapshot every 15 s (clamped 5–60), PCR, minute history |
| 8 | **SignalWorker** | Entry pipeline, 1 s cycle per symbol |
| 9 | **ExitWorker** | Exit pipeline, 1 s cycle |
| 10 | TelegramWorker / WhatsAppWorker | Notifications |
| 11 | DailyPnlWorker | Daily PnL |
| 12 | **BrokerPositionSyncWorker** | Reconcile positions with broker |
| 13 | **BrokerOrderSyncWorker** | Reconcile orders with broker (every 30 s) — *source of truth* |
| 14 | TickRetentionWorker | Purge old ticks |
| 15 | IndicatorSnapshotOutcomeWorker | Analytics only; off unless `EnableOutcomeTracking` |

## 3. Data layer (before any decision)

```
Groww feed ─► MarketFeedWorker ─► MarketTicks
Groww hist ─► HistoricalDataWorker ─► 1m / 5m candles ─► CandleAggregator
candles ────► EmaWorker ─► EMA20 / EMA50      (ATR, RSI, VWAP, PCR computed in SignalEngine services)
Groww chain ► OptionChainWorker ─► OptionChainData (+ OptionMinuteHistory), PCR, Delta, OI, ΔOI
```

`MarketSnapshotBuilder.BuildAsync` fuses these into one `MarketSnapshot` (EMA20/50, ATR, RSI 1m/5m,
PCR, option chain, Market Regime incl. gap flags) and creates the `IndicatorSnapshot` audit row.

## 4. Entry pipeline — `SignalWorker` (every 1 s, per active symbol, inside signal window)

```
MarketHours.IsSignalWindow? ──no──► sleep 1 min
        │yes
        ▼
 1 MarketSnapshotBuilder        (null → skip)
        ▼
   EMA ready? (skipped on expiry day) ──no──► audit BlockedBy=IndicatorsNotReady
        ▼
 2 PRE-GATES (order matters; first block wins, written to audit)
     KillSwitch → EventBlackout → StaleData → ConsecutiveLoss → OnePosition → ATR → MarketRegime*
     (* skipped on expiry day; MaxDailyTrades and CircuitBreaker exist but are commented out)
        ▼
 3 InstrumentStrategyRegistry → DefaultInstrumentStrategy → StrategyManager.SelectSignalAsync
        │
        ├─ BreakoutProtection.Update(candles)
        ├─ StrategySelectorService.Select  ("one brain")
        │     News → none │ StrongTrend → none │ Range/Reversal/NoTrade → none
        │     Trend/Breakout/Breakdown/Scalping → Directional
        │     Expiry day: before cutoff → Directional, after → HeroZero
        │     (BlockRangeMarketState default ON)
        ├─ Directional → CALL / PUT entry evaluators (EMA20/50, RSI, PCR, Delta, OI, candle, ATR)
        │  HeroZero    → strike scoring & ranking (Greeks, OI, volume, liquidity, momentum, trend)
        ├─ fallbacks: EarlyCross CALL (paper only) · approved SingleSignalRule rules
        ├─ optional 5-min confirmation, confidence floor (Directional only, default 0), breakout protection
        └─ AdaptiveLotSizingService → quantity
        ▼
 4 POST-GATES (17, in order)
     WhipsawShadow(diag) → OiConfirmation → DuplicateSignal → Risk → IndexStopFloor → StaleEntry
     → EarlyTrend → Chop → FastEma → SessionTrend → CrossIndexAgreement → MarketDirection
     → VwapSide → HandoffCooldown → PutS1 → RiskReward → Spread
        ▼
 5 SignalOrderPlacementService.PersistAndPlaceAsync
 finally: audit row saved exactly once (BlockedBy / BlockedReason / confidence / strategy)
```

Almost every gate is a `StrategySettings` flag/threshold (per-symbol → global → code default,
cached ~5 min), so behaviour changes without redeploy.

## 5. Order placement (crash-safe contract)

```
cap qty at MaxLotSize → stamp RSI(5m)/PCR/Delta/ΔOI/ATR on signal
Signal.Status = PENDING, OrderReferenceId = GUID  ──save──┐
Order row (BUY, MIS, LIMIT or MARKET, PENDING)      ──save──┤  BEFORE broker call
                                                            ▼
                                              Groww PlaceOrder
   ┌──────────────────────┬──────────────────────────────┬─────────────────────────────┐
   ▼ ambiguous (timeout/5xx) ▼ definite failure             ▼ accepted
 Order=ORDER_UNKNOWN       Signal=ORDER_REJECTED          Signal=ORDER_PLACED, BrokerOrderId saved
 Signal stays PENDING      Order=FAILED                   (if local save fails → ORDER_UNKNOWN,
 critical alert            critical alert                  never downgraded to rejected)
 → reconciled by OrderReferenceId by BrokerOrderSyncWorker
        then SignalR broadcast to UI
```

## 6. Fill confirmation — `BrokerOrderSyncWorker` (30 s, source of truth)

- Known `BrokerOrderId` → `GetOrderStatusAsync`; unknown → `GetOrderByReferenceAsync` (max 10 attempts,
  then `MANUAL_CHECK_REQUIRED`).
- Entry **COMPLETE** → `ActivateSignalAsync`: Signal `ACTIVE`, create STRATEGY `Position` (guarded by
  `EntryOrderId`, no duplicates), re-anchor SL/T1/T2 to the real fill by ratio, clear stale
  MFE/MAE/ProtectedSL, **place broker SL_M stop-loss** (single-leg / HERO_ZERO only).
- Entry unfilled past per-symbol timeout → confirm-then-cancel → clean miss (frees `OnePositionGate`).
- Rejected/cancelled **entry** → `ORDER_REJECTED`; rejected **exit** → back to `ACTIVE` so exit retries.
- Dead SL_M (rejected/cancelled) → clear id so ExitWorker self-heals; guarded against clobbering a newer stop.
- Exit **COMPLETE** → signal CLOSED, TradeJournal closed, position closed (`TradeCompletionService`).

## 7. Exit pipeline — `ExitWorker` (1 s, session hours)

```
load open signals (ExitDataLoader) → none? return
live option quotes + ticks + chain + latest ATR
    │
    ▼ per open signal
  skip if still ORDER_PLACED (unfilled) unless force-exit
  resolve LTPs (option / leg2 / delta); skip spread w/ missing leg2 unless force-exit
  TrailingStopService: MFE/MAE + ATR trailing stop  → SAVE (before decision, can throw)
  StopLossSyncService: broker SL_M self-heal / replace on trailing move (live, single-leg, not force-exit)
  ExitDecisionService ladder (first match wins):
     1 HeroZero safety (premium < ₹2)        5 Option SL (broker SL_M owns it; bounded MARKET fallback)
     2 Force exit EOD                         6 Index SL
     3 Index T1 → no exit, lock ProtectedSL   7 Option T2 → full exit
     4 Protected SL (after Index T1)          8 Option T1 → partial (if >1 lot)
     (optional target ladder replaces 7/8: PARTIAL_T1 50% → PARTIAL_T2/T3 → OPTION_T5 full)
    │ exitReason?
    ├─ PAPER: TradeCompletionService.CompleteExitAsync + PaperTradingService.CloseBySymbol
    └─ LIVE : PositionCloseService.ClosePositionAsync → cancels resting SL_M, sends SELL
              (NO journal here — BrokerOrderSyncWorker finalizes on COMPLETE)
```

## 8. Position and order lifecycle

```
Signal:  PENDING → ORDER_PLACED → ACTIVE → (PARTIAL_EXIT_PENDING ↔ ACTIVE) → EXIT_PENDING → CLOSED
                 ↘ ORDER_REJECTED / MANUAL_CHECK_REQUIRED
Order:   PENDING → COMPLETE | REJECTED | CANCELLED | ORDER_UNKNOWN → MANUAL_CHECK_REQUIRED
Purpose: Entry · StopLoss(SL_M resting, or MARKET fallback) · Target/partial · Force exit
Journal: opened at entry → closed only by BrokerOrderSyncWorker after SELL COMPLETE
```

`BrokerPositionSyncWorker` independently compares local qty vs broker qty (BROKER_SYNC rows) to catch
manual closes/drift.

## 9. Safety layers (must never be removed)

Duplicate-order protection (OrderReferenceId, DuplicateSignalGate, EntryOrderId guard) ·
Pre/post risk gates, kill switch, event blackout, ConsecutiveLoss · One-position rule · Broker-managed
SL_M · Trailing + Protected SL · Force exit EOD · Row-before-broker-call · Ambiguous-order reconciliation ·
Position sync · TradeJournal · `AppErrors` logging that never crashes a worker · Full Groww request logging.

## 10. Configuration and control

| Where | Holds |
|---|---|
| `StrategySettings` table | thresholds + every `Enable<X>` flag, per symbol, ~5 min cache |
| `SystemSettings` table | `AutoTradingEnabled`, `TradingMode` (paper/live), Groww token, checkpoints — contains credentials |
| `appsettings.json` | compiled defaults |

Paper vs live is decided per signal (`orders.IsPaperSignal`); paper signals skip broker and SL_M.

## 11. Observations worth a look (from reading, not changes)

1. `ExitWorker` computes `dailyLossHit` every second but never uses it (code comment says so) — wasted queries.
2. `MaxDailyTradesGate` and `CircuitBreakerGate` are commented out of `PreGates`, so there is no daily
   trade cap or circuit breaker at gate level (only `ConsecutiveLossGate`).
3. `ExitWorker` returns early when `chainBySymbol.Count == 0` outside force-exit, so a chain outage pauses
   trailing/target logic (broker SL_M still protects).
4. Trading.Web (Angular) is not in this checkout, so the UI side of the flow is not covered.
