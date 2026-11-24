using Microsoft.Extensions.Logging;
using Trading.Application.Interfaces;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Scoring.Ranking;
using Trading.SignalEngine.Scoring.Services;
using Trading.SignalEngine.Services;
using Trading.SignalEngine.Strategies;
using Trading.Worker.SignalGates;

namespace Trading.Worker;

/// <summary>
/// Single entry point for engine selection.
/// SignalWorker calls SelectSignalAsync() — builds a StrategyContext and delegates the
/// actual decision to StrategySelectorService (the "one brain"), then dispatches to
/// whichever concrete engine it picked: Expiry | HeroZero | Directional | none.
/// </summary>
/// <summary>
/// Additive result wrapper around SelectSignalAsync — carries MarketState/Strategy/Confidence/
/// BlockedReason for the Market Snapshot Audit feature even when Signal is null (suppressed by
/// StrategySelectorService, the CALL/PUT chain, the confidence floor, the 5m confirmation, or
/// lot sizing). Does not change any decision made inside SelectSignalAsync — only what gets
/// returned to the caller.
/// </summary>
public record SignalSelectionResult(
    Signal? Signal,
    MarketState MarketState,
    SelectedStrategy Strategy,
    decimal Confidence,
    string? BlockedReason,
    string? BlockedBy,
    // Set only when an approved SingleSignalRuleEvaluator rule produced this signal (null when
    // the CALL/PUT chain produced it). Carried for logging/audit — never used as a trading input.
    string? RuleId = null);

public class StrategyManager
{
    private readonly HeroZeroScoreService _heroScorer;
    private readonly StrikeRankingService _heroRanking;
    private readonly StrategySelectorService _selector;
    private readonly MarketClassificationService _classifier;
    private readonly AdaptiveLotSizingService _lotSizing;
    private readonly SingleSignalRuleEvaluator _ruleEvaluator;
    private readonly FiveMinuteConfirmationService _fiveMinuteConfirmation;
    private readonly IRepository<StrategyExecutionLog> _executionLogRepo;
    private readonly IUnitOfWork _uow;
    private readonly BreakoutProtectionService _breakoutProtectionService;
    private readonly ILogger<StrategyManager> _logger;

    public StrategyManager(
        HeroZeroScoreService heroScorer,
        StrikeRankingService heroRanking,
        StrategySelectorService selector,
        MarketClassificationService classifier,
        AdaptiveLotSizingService lotSizing,
        SingleSignalRuleEvaluator ruleEvaluator,
        FiveMinuteConfirmationService fiveMinuteConfirmation,
        IRepository<StrategyExecutionLog> executionLogRepo,
        IUnitOfWork uow,
        BreakoutProtectionService breakoutProtectionService,
        ILogger<StrategyManager> logger)
    {
        _heroScorer = heroScorer;
        _heroRanking = heroRanking;
        _selector = selector;
        _classifier = classifier;
        _lotSizing = lotSizing;
        _ruleEvaluator = ruleEvaluator;
        _fiveMinuteConfirmation = fiveMinuteConfirmation;
        _executionLogRepo = executionLogRepo;
        _uow = uow;
        _breakoutProtectionService = breakoutProtectionService;
        _logger = logger;
    }

    internal static bool ShouldBlockRangeMarket(MarketState state, bool blockRange)
        => blockRange && state == MarketState.Range;

    public async Task<SignalSelectionResult> SelectSignalAsync(SignalContext ctx, MarketSnapshot market, string symbol)
    {
        _breakoutProtectionService.Update(
   symbol,
   market.Candles1m);
        var strategyContext = await BuildStrategyContextAsync(ctx, market, symbol);
        var decision = await _selector.Select(strategyContext);

        // Early-cross CALL (paper only, off by default): evaluated BEFORE the Range block so it can act in the first minutes of
        // a turn the selector still labels Range/Reversal. Returns null in every other case (flag off, not Paper, no match).
        var earlySignal = TryGenerateEarlyCrossSignal(ctx, market, symbol, out var earlyRuleId);

        // Range = no clear trend: no trade at all. The selector already yields "no strategy" for Range on normal days,
        // but that alone still reaches the approved-rule fallback below and is bypassed on expiry days (the selector
        // dispatches those by time of day), so the state is stopped here, before either. Switch: BlockRangeMarketState
        // (default on; an active row with value 0 turns it off).
        if (earlySignal is null && ShouldBlockRangeMarket(decision.MarketState, ctx.StrategySettings.GetBool("BlockRangeMarketState", symbol, true)))
        {
            return new SignalSelectionResult(null, decision.MarketState, decision.Strategy, 0m,
                $"MarketState=Range - no trade ({strategyContext.MarketStateReason})", "StrategySelector");
        }

        var engineResult = decision.Strategy switch
        {
            SelectedStrategy.HeroZero => await RunHeroZero(ctx, market, symbol),
            SelectedStrategy.Directional => RunDirectional(ctx, market, symbol, decision.MarketState),
            _ => SignalGenerationResult.Failed(decision.Reason)
        };

        // StrategySelector only ever gets blamed for "no strategy chosen" (Strategy == None).
        // Once a concrete engine actually ran, any further suppression — the engine's own
        // conditions, the confidence floor, the 5m confirmation, or lot sizing — is the
        // SignalEngine's call.
        var blockedBy = decision.Strategy == SelectedStrategy.None ? "StrategySelector" : "SignalEngine";

        var signal = engineResult.Signal;
        string? ruleId = null;
        if (signal == null && earlySignal != null)
        {
            signal = earlySignal;
            ruleId = earlyRuleId;
        }

        // ── 5-minute market confirmation ─────────────────────────────────────────────────────
        // Applied HERE, the single point every path converges on — Directional, HeroZero and the
        // approved-rule fallback below all produce their Signal through this method — so the
        // confirmation exists in exactly one place rather than being duplicated per engine.
        //
        // The 1-minute entry logic inside the engines is untouched and remains the final trigger;
        // this only asks whether the 5m environment supports the direction the 1m logic chose.
        // Off unless UseFiveMinuteConfirmation is set for the symbol, in which case this block is
        // skipped entirely and behaviour is identical to before.
        var fiveMinuteEnabled = ctx.StrategySettings.GetBool(
            FiveMinuteConfirmationService.EnabledSettingKey, symbol, false);

        // ── Approved single-signal rule fallback ─────────────────────────────────────────────
        //   if the CALL/PUT chain produced a signal          -> use it
        //   else if an approved rule matches                 -> generate that signal
        //   else                                             -> no signal
        //
        // Reached in two cases: the CALL/PUT chain rejected outright, or it produced a signal
        // below the confidence floor. The rule's conditions were validated as a complete set, so
        // neither the chain nor the floor may veto it. Every OTHER protection still runs below and
        // after this: lot sizing, post-gates (duplicate, cooldown, OI, risk, R:R, spread),
        // open-position and pre-gate checks.
        //
        // CONFIDENCE FLOOR - DB-TUNABLE, AND DISABLED (0 = no floor).
        //
        // Kept as a setting rather than deleted so it can be reinstated without a rebuild, but it
        // is set to 0 deliberately and should stay there. Measured over 45,665 snapshots that
        // reached this check: only 6 ever cleared 70, i.e. the floor rejected 98.7% of everything
        // the engine produced, and the survivors' win rate rested on 5 and 1 observations. Live
        // signals agreed - below 70 went 3W/2L, at-or-above 70 went 4W/7L. No measured
        // discriminating power.
        //
        // It is also redundant by construction: ComputeConfidence scores EMA separation, PCR, RSI
        // band and candle body, every one of which the CALL/PUT chain already enforces as a hard
        // AND-condition. And it spans two incompatible scales - ComputeConfidence tops out near 85
        // after the volume term was dropped, while HeroZero writes a 0-100 score into the same
        // Signal.Confidence field, yet one threshold would apply to both.
        //
        // RAISING THIS ABOVE 0 RE-ENABLES A FILTER WITH NO EVIDENCE BEHIND IT. Position size does
        // not read confidence either - AdaptiveLotSizingService fixes quantity at AdaptiveLotMin
        // regardless of score.
        //
        // SCOPE: this floor is NOT directional-only. It sits AFTER the engine switch above, so
        // `signal` here may have come from HeroZero just as easily as from the CALL/PUT chain, and
        // the same threshold is applied to both. HeroZero writes a 0-100 score into
        // Signal.Confidence while ComputeConfidence tops out near 85, so one number would mean two
        // different things. At 0 that is harmless - nothing is filtered on either path - but
        // raising it would silently gate HeroZero too. A per-engine floor would need separate
        // keys; there is deliberately no evidence to justify either one today.
        // Two keys, deliberately. MinSignalConfidence applies to EVERY engine and stays at 0;
        // MinDirectionalConfidence applies ONLY when StrategySelector chose Directional. Without
        // the split, a floor set for the CALL/PUT chain would also gate HeroZero, whose score is a
        // 0-100 scale written into the same Signal.Confidence field that ComputeConfidence tops
        // out near 85 - one number meaning two different things.
        // ONE confidence key, DIRECTIONAL ONLY. MinSignalConfidence has been dropped: it was a
        // second floor covering every engine, so a value set for the CALL/PUT chain also gated
        // HeroZero - whose score is a 0-100 scale written into the same Signal.Confidence field
        // that ComputeConfidence tops out near 85. One number meaning two different things.
        //
        // HeroZero is therefore never confidence-gated. Deactivating MinDirectionalConfidence
        // (or setting it to 0) removes the floor from Directional too.
        var minConfidence = 0m;

        if (decision.Strategy == SelectedStrategy.Directional
            && ctx.StrategySettings.IsSettingActive("MinDirectionalConfidence", symbol))
        {
            minConfidence = ctx.StrategySettings.GetDecimal("MinDirectionalConfidence", symbol, 55m);
        }

        if (signal == null || signal.Confidence < minConfidence)
        {
            var ruleSignal = TryGenerateRuleSignal(ctx, market, symbol, decision, out ruleId);
            if (ruleSignal != null)
            {
                signal = ruleSignal;
            }
            else if (signal == null)
            {
                var reason = engineResult.FailureReasons.Count > 0
                    ? string.Join(" | ", engineResult.FailureReasons)
                    : decision.Reason;
                return new SignalSelectionResult(null, decision.MarketState, decision.Strategy, 0m, reason, blockedBy);
            }
            else
            {
                return new SignalSelectionResult(null, decision.MarketState, decision.Strategy, signal.Confidence,
                    $"Confidence {signal.Confidence:F0} below minimum {minConfidence:F0}", blockedBy);
            }
        }
        // After approved-rule fallback has finished.
        // At this point, signal may come from Directional, HeroZero, EarlyCross or an approved rule.

        // Breakout protection — applies to all signal-generation paths when StrategySettings.EnableBreakoutProtection is on
        // (default off: no row = not applied, the same as before the service was registered).
        if (signal != null && ctx.StrategySettings.GetBool(BreakoutProtectionService.EnableKey, symbol, false))
        {
            var isCall = signal.SignalType == TradingConst.SignalType.CALL;

            var breakoutDecision = _breakoutProtectionService.Evaluate(
                symbol,
                isCall,
                market.Candles1m);

            if (!breakoutDecision.Allowed)
            {
                return new SignalSelectionResult(
                    null,
                    decision.MarketState,
                    decision.Strategy,
                    signal.Confidence,
                    breakoutDecision.Reason,
                    "BreakoutProtection",
                    ruleId);
            }
        }
       

        // Existing five-minute confirmation continues unchanged.
        if (fiveMinuteEnabled)
        {
            // Existing code...
        }
        if (fiveMinuteEnabled)
        {
            var isCall = signal.SignalType == TradingConst.SignalType.CALL;
            var confirmation = _fiveMinuteConfirmation.Confirm(
                isCall, market.Ema20_5m, market.Ema50_5m, market.Rsi5m, ctx.Settings);

            LogTimeframeDecision(symbol, market, decision, signal, confirmation, ruleId);

            if (!confirmation.Passed)
            {
                return new SignalSelectionResult(null, decision.MarketState, decision.Strategy, signal.Confidence,
                    $"BLOCKED_BY_5M_CONFIRMATION — {confirmation.Reason}", "FiveMinuteConfirmation");
            }
        }

        var lotDecision = await _lotSizing.DetermineAsync(ctx, market, signal.Confidence);
        if (lotDecision.Lots <= 0)
        {
            return new SignalSelectionResult(null, decision.MarketState, decision.Strategy, signal.Confidence,
                lotDecision.Reason, blockedBy);
        }
        signal.Quantity = lotDecision.Lots;
        signal.SelectedStrategy = ruleId == EarlyCrossCallRuleEvaluator.RuleId
            ? EarlyCrossCallRuleEvaluator.StrategyTag
            : decision.Strategy.ToString();
        signal.MarketState = decision.MarketState.ToString();
        signal.IsExpiryDay = strategyContext.IsExpiryDay;

        await LogExecutionAsync(symbol, strategyContext, decision, signal, ruleId);

        return new SignalSelectionResult(signal, decision.MarketState, decision.Strategy, signal.Confidence, null, null, ruleId);
    }

    // ── Early-cross CALL (paper only) ─────────────────────────────────────────────────────────
    // One settings lookup per cycle when the flag is off (the default). Uses the last closed 1m bar as the trigger on purpose:
    // the default trigger candle in Paper is the 5m bar, but the tested rule is a 1m-turn rule.
    private Signal? TryGenerateEarlyCrossSignal(SignalContext ctx, MarketSnapshot market, string symbol, out string? ruleId)
    {
        ruleId = null;
        if (!_ruleEvaluator.IsEarlyCrossEnabled(symbol))
            return null;

        var bar = market.LastClosed1mCandle;
        var input = new EarlyCrossInput(
            ctx.Orders?.CurrentMode == TradingMode.Paper,
            EarlyCrossCallRuleEvaluator.BarsSince1mUp(market.Candles1m),
            market.Ltp, market.Ema20, market.Ema50, market.Ema20_5m, market.Ema50_5m, market.Rsi5m,
            bar is not null && bar.Close > bar.Open);

        var match = _ruleEvaluator.EvaluateEarlyCross(symbol, input);
        if (!match.Matched)
        {
            _logger.LogDebug("EARLY CROSS SUPPRESSED Symbol={Symbol} Reason={Reason}", symbol, match.Reason);
            return null;
        }

        var result = ctx.Engine.GenerateRuleSignal(
            symbol, match.OptionType!, match.Call, market.Ltp, market.Ema20, market.Ema50, market.PcrValue, market.Rsi,
            market.CurrentVolume, market.AvgVolume20, market.AtrRaw, market.OptionChain, bar, out _,
            RiskAtr(ctx, market, symbol));

        if (result.Signal == null)
            return null;

        ruleId = match.RuleId;
        return result.Signal;
    }

    // ── Approved single-signal rule evaluation ───────────────────────────────────────────────
    // Returns the built Signal when an approved rule matched AND the option leg resolved, else
    // null. Never throws past the caller: an option-chain failure is logged, not swallowed, and
    // simply means no rule signal this cycle.
    private Signal? TryGenerateRuleSignal(
        SignalContext ctx,
        MarketSnapshot market,
        string symbol,
        StrategyDecision decision,
        out string? ruleId)
    {
        ruleId = null;

        var snapshot = BuildRuleSnapshot(market, symbol, decision.MarketState);

        // 1-MINUTE ENTRY TRIGGER. Resolved through the SAME mechanism the AND-chain uses
        // (SignalEngineService.ConfirmEntryCandle = direction + BodyPctMinimum) against the SAME
        // closed candle ResolveTriggerCandle picks. The 5m legs inside the rule can only PERMIT a
        // direction; this is what fires it.
        var triggerCandle = ResolveTriggerCandle(ctx, market, symbol);
        // BodyPctMinimum is the one setting that keeps the plain fall-back-to-default behaviour:
        // IsActive = 0 still applies the compiled 35 default rather than skipping the body-
        // strength floor. Every other decimal setting in this class instead skips its check
        // entirely when inactive (see MinDirectionalConfidence above).
        var bodyPctMinimum = ctx.StrategySettings.GetDecimal("BodyPctMinimum", symbol, 35m);
        var (entryBullish, entryBearish) =
            SignalEngineService.ConfirmEntryCandle(triggerCandle, bodyPctMinimum);

        var match = _ruleEvaluator.Evaluate(snapshot, entryBullish, entryBearish);

        if (!match.Matched)
        {
            // Debug only — this runs on most cycles and must never flood Information logs.
            _logger.LogDebug(
                "SINGLE RULE SUPPRESSED Symbol={Symbol} Reason={Reason}",
                symbol, match.Reason);
            return null;
        }

        var result = ctx.Engine.GenerateRuleSignal(
            symbol,
            match.OptionType!,
            match.Call,
            market.Ltp,
            market.Ema20,
            market.Ema50,
            market.PcrValue,
            market.Rsi,
            market.CurrentVolume,
            market.AvgVolume20,
            market.AtrRaw,
            market.OptionChain,
            triggerCandle,
            out var confidence,
            RiskAtr(ctx, market, symbol));

        if (result.Signal == null)
        {
            return null;
        }

        ruleId = match.RuleId;


        return result.Signal;
    }

    // ── Timeframe decision log ───────────────────────────────────────────────────────────────
    // One Information line per candidate trade, emitted only when the confirmation feature is on
    // AND a 1m entry trigger actually produced a signal — so it cannot flood on ordinary cycles.
    // Carries both timeframes side by side so a blocked trade is explainable from the log alone.
    //
    // MarketState and MarketRegime are reported as 1m values and labelled as such: no 5-minute
    // equivalent of either exists in this system (see FiveMinuteConfirmationService).
    private void LogTimeframeDecision(
        string symbol,
        MarketSnapshot market,
        StrategyDecision decision,
        Signal signal,
        FiveMinuteConfirmation confirmation,
        string? ruleId)
    {
       
    }

    // ── Entry trigger candle ─────────────────────────────────────────────────────────────────
    // Which CLOSED candle supplies body%/direction confirmation. The 5m EMA trend filter is
    // unaffected either way, and the still-forming bar is never a candidate — both series
    // exclude it in MarketSnapshotBuilder.
    //
    // The default keys off "is this real money", NOT off Live specifically. TradingMode.HeroZero
    // is Live order execution with the lot size forced to 1 (HeroZeroOrderStrategy derives from
    // LiveOrderStrategy and overrides only GetLots), so it is the same entry strategy and must use
    // the same trigger candle. Testing CurrentMode == Live silently gave HeroZero mode a
    // 5-minute trigger while it placed real orders.
    //
    // Setting an explicit UseOneMinuteEntryCandle row removes the mode dependency entirely and is
    // the recommended configuration — see docs/set-one-minute-entry-trigger.sql.
    private static Candle? ResolveTriggerCandle(SignalContext ctx, MarketSnapshot market, string symbol) =>
        UseOneMinuteTrigger(ctx.Orders.CurrentMode, ctx.StrategySettings, symbol)
            ? market.LastClosed1mCandle
            : market.LastClosed5mCandle;

    /// <summary>
    /// True when the 1-minute closed candle is the entry trigger. Takes the mode explicitly rather
    /// than a SignalContext so the rule is directly testable.
    /// </summary>
    internal static bool UseOneMinuteTrigger(
        TradingMode mode, IStrategySettingsService strategySettings, string symbol)
    {
        var defaultToOneMin = mode != TradingMode.Paper;
        return strategySettings.GetBool("UseOneMinuteEntryCandle", symbol, defaultToOneMin);
    }

    // Builds the row the rule is evaluated against. Deliberately an IndicatorSnapshot: that is the
    // exact shape the historical validation queries, so the live rule and the backtest read the
    // same fields. TrendDirection is taken from the audit row MarketSnapshotBuilder already
    // produced (the value that gets persisted), falling back to the identical expression only if
    // no audit row exists.
    private static IndicatorSnapshot BuildRuleSnapshot(MarketSnapshot market, string symbol, MarketState marketState) =>
        new()
        {
            Symbol = symbol,
            TrendDirection = market.Audit?.TrendDirection
                ?? (market.Ema20 > market.Ema50 ? "Bullish" : market.Ema20 < market.Ema50 ? "Bearish" : "Sideways"),
            MarketState = marketState.ToString(),
            Ltp = market.Ltp,
            Ema20 = market.Ema20,
            Ema50 = market.Ema50,
            Ema20_5m = market.Ema20_5m,
            Ema50_5m = market.Ema50_5m,
            Rsi = market.Rsi,
            Rsi5m = market.Rsi5m,
            AtrRaw = market.AtrRaw,
            AtrPct = market.AtrPct,
            Pcr = market.PcrValue,
            PriceChangePct = market.PriceChangePct,
        };

    // ── Explainable decision summary — MarketState/strategy/indicators/historical win rate/confidence ──
    private static string BuildExplainableReason(StrategyContext ctx, StrategyDecision decision, Signal signal)
    {
        var emaDescriptor = decision.MarketState switch
        {
            MarketState.StrongTrend => "Strong",
            MarketState.Trend => "Trend",
            _ => "Flat"
        };

        var historical = decision.HistoricalWinRate.HasValue
            ? $"{decision.HistoricalWinRate.Value:F0}% (n={decision.HistoricalTrades})"
            : "n/a";

        return $"MarketState={decision.MarketState} Selected={decision.Strategy} EMA={emaDescriptor} " +
               $"ATR={ctx.Atr:F2} RSI={ctx.Rsi:F0} PCR={ctx.Pcr:F2} HistoricalWinRate={historical} " +
               $"Confidence={signal.Confidence:F0}";
    }

    // ── Build the pure decision snapshot ─────────────────────────────────────
    private async Task<StrategyContext> BuildStrategyContextAsync(SignalContext ctx, MarketSnapshot market, string symbol)
    {
        var isHighImpactEvent = market.NewsEvent != null;
        var isExpiryDay = ctx.Instruments.IsExpiryDay(symbol);

        // Computed once here so the selector itself can stay a pure function over primitives —
        // HeroZeroSignalEngine.Generate re-scores when it actually runs; the small duplicate
        // work is the accepted tradeoff for keeping StrategySelectorService dependency-free.
        var settings = await ctx.SystemSettings.GetAsync();
        var (ceScores, peScores) = _heroScorer.Score(
            symbol, market.OptionChain, market.Ema20, market.Ema50, market.Ema20_5m, market.Ema50_5m,
            market.PriceChangePct, market.Candles5m, settings);
        var heroResult = _heroRanking.Rank(ceScores, peScores);
        var heroTopScore = Math.Max(heroResult.HeroCe?.TotalScore ?? 0m, heroResult.HeroPe?.TotalScore ?? 0m);

        var classification = _classifier.Classify(
            symbol, market.Ema20, market.Ema50, market.Ema20_5m, market.Ema50_5m,
            market.AtrRaw, market.Rsi, market.Ltp, market.PriceChangePct,
            market.MarketRegime.IsGapUp, market.MarketRegime.IsGapDown, isHighImpactEvent, settings);

        return new StrategyContext
        {
            Symbol = symbol,
            Ltp = market.Ltp,
            Ema20 = market.Ema20,
            Ema50 = market.Ema50,
            Rsi = market.Rsi,
            Pcr = market.PcrValue,
            Atr = market.AtrRaw,
            IsExpiryDay = isExpiryDay,
            IsHighImpactEvent = isHighImpactEvent,
            Regime = market.MarketRegime,
            OptionChain = market.OptionChain,
            HeroZeroTopScore = heroTopScore,
            MarketState = classification.State,
            MarketStateReason = classification.Reason
        };
    }

    // ── Fix 2: index stop from 5-minute ATR ──────────────────────────────────────────────────
    // The index stop was 1.5 x ATR(14) on 1-MINUTE candles (floored at 20 pts), which sits inside one
    // 5-minute bar's normal swing in a choppy market. With EnableAtr5mStop on, the stop becomes
    // Atr5mStopMultiplier x ATR(14) on closed 5m bars. AtrRiskService multiplies whatever ATR it is given
    // by AtrStopLossMultiplier, so the 5m ATR is pre-divided by that multiplier here; the min/max clamp
    // and the target ratios in AtrRiskService are unchanged. Returns 0 (= keep the 1m ATR, the previous
    // behaviour) when the flag is off or there are not enough 5m bars.
    //   EnableAtr5mStop      0/1      (default 0)
    //   Atr5mStopMultiplier  decimal  (default 1.0)
    internal static decimal RiskAtr(SignalContext ctx, MarketSnapshot market, string symbol)
    {
        if (!ctx.StrategySettings.GetBool("EnableAtr5mStop", symbol, false))
            return 0m;

        var atr5m = FiveMinuteBars.Atr(market.Candles5m, 14);
        if (atr5m <= 0m)
            return 0m;

        var multiplier = ctx.StrategySettings.GetDecimal("Atr5mStopMultiplier", symbol, 1.0m);
        var stopMultiplier = ctx.Settings.AtrStopLossMultiplier > 0 ? ctx.Settings.AtrStopLossMultiplier : 1.5m;

        return atr5m * multiplier / stopMultiplier;
    }

    // ── Engine runners ────────────────────────────────────────────────────────
    private SignalGenerationResult RunDirectional(
        SignalContext ctx, MarketSnapshot market, string symbol, MarketState marketState)
    {
        var triggerCandle = ResolveTriggerCandle(ctx, market, symbol);

        // Confidence floor is applied once, centrally, in SelectSignalAsync — not here.
        // marketState feeds the CALL-side StrongTrend gate (see SignalEngineService).
        return ctx.Engine.GenerateSignalDetailed(
            symbol, market.Ltp, market.Ema20, market.Ema50,
            market.PcrValue, market.Rsi, market.CurrentVolume, market.AvgVolume20,
            market.PriceChangePct, market.AtrPct, market.AtrRaw, market.OptionChain,
            market.StrategyHint, market.Ema20_5m, market.Ema50_5m, triggerCandle,
            marketState,
            // CALL-side inputs. Vwap is null for indices (no volume from Groww) and passes through
            // as 0, which the VWAP condition treats as unavailable.
            vwap: market.Vwap ?? 0m,
            prev5mHigh: market.LastClosed5mCandle?.High ?? 0m,
            prev5mLow: market.LastClosed5mCandle?.Low ?? 0m,
            dayOpen: market.DayOpen,
            dayLow: market.DayLow,
            dayHigh: market.DayHigh,
            dayChangePct: market.DayChangePct,
            // Entry RSI = 5m RSI(14) on closed bars; null fails the RSI condition closed.
            rsi5m: market.Rsi5m,
            yesterdayClose: market.YesterdayClose,
            riskAtr: RiskAtr(ctx, market, symbol));
    }

    private async Task<SignalGenerationResult> RunHeroZero(SignalContext ctx, MarketSnapshot market, string symbol)
    {
        var signal = await ctx.HeroZeroEngine.Generate(
            symbol, market.Ltp, market.Ema20, market.Ema50,
            market.PcrValue, market.Rsi, market.CurrentVolume, market.AvgVolume20,
            market.PriceChangePct, market.AtrRaw, market.Ema20_5m, market.Ema50_5m, market.Candles5m,
            ctx.Instruments.IsExpiryDay(symbol), market.OptionChain);
        return signal != null
            ? SignalGenerationResult.Success(signal)
            : SignalGenerationResult.Failed("HeroZero engine did not generate a signal");
    }

    // ── Audit log — best-effort, never blocks signal generation ──────────────
    private async Task LogExecutionAsync(
        string symbol, StrategyContext strategyContext, StrategyDecision decision, Signal signal, string? ruleId = null)
    {
        var explainable = BuildExplainableReason(strategyContext, decision, signal);

        // Signal has no RuleId column, so the approved-rule attribution is carried in this audit
        // row's Reason. Adding a real column needs a migration and separate approval.
        if (ruleId != null)
            explainable = $"RuleId={ruleId} {explainable}";

        try
        {
            await _executionLogRepo.AddAsync(new StrategyExecutionLog
            {
                CreatedOn = IstClock.Now,
                Symbol = symbol,
                SelectedStrategy = signal.SelectedStrategy ?? decision.Strategy.ToString(),
                MarketState = decision.MarketState.ToString(),
                Reason = explainable,
                Score = signal.Confidence
            });
            await _uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Best-effort audit row — must never block signal generation, but the persistence
            // failure is no longer swallowed silently.
            _logger.LogError(ex,
                "Failed to write StrategyExecutionLog for {Symbol} (RuleId={RuleId})", symbol, ruleId);
        }
    }

}
