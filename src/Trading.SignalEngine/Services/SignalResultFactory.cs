using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Services;

namespace Trading.SignalEngine.Services;

/// <summary>
/// Option-leg lookup, index/option SL/target math, and SignalBuilder assembly — the last step
/// shared by both the normal AND-chain path (<see cref="SignalEngineService.GenerateSignalDetailed"/>)
/// and the pre-approved single-signal-rule path (<see cref="SignalEngineService.GenerateRuleSignal"/>).
/// </summary>
public class SignalResultFactory
{
    private readonly IInstrumentCache _instrumentCache;
    private readonly AtrRiskService _atrRisk;
    private readonly SystemSettingsService _settingsService;
    private readonly OptionLegSelector _legSelector;

    public SignalResultFactory(
        IInstrumentCache instrumentCache,
        AtrRiskService atrRisk,
        SystemSettingsService settingsService,
        OptionLegSelector legSelector)
    {
        _instrumentCache = instrumentCache;
        _atrRisk = atrRisk;
        _settingsService = settingsService;
        _legSelector = legSelector;
    }

    // Resolved once per call — cached 60s in SystemSettingsService
    private TradingSettings Settings => _settingsService.GetAsync().GetAwaiter().GetResult();

    public SignalGenerationResult Build(
        string symbol, decimal ltp, decimal atrRaw, List<OptionChainData> chain, decimal atmStrike,
        string optionType, decimal confidence, bool call)
    {
        var chainLeg = _legSelector.SelectLiquidLeg(symbol, chain, optionType, atmStrike);
        var optionLtp = chainLeg?.Ltp ?? 0m;

        // No valid premium — chain data stale or strike not found. Block signal.
        if (optionLtp <= 0)
            return SignalGenerationResult.Failed("Stale/zero option premium for selected leg");

        // Prefer Groww's TradingSymbol; fall back to constructed name
        var optionSymbol = (chainLeg != null && !string.IsNullOrWhiteSpace(chainLeg.TradingSymbol))
            ? chainLeg.TradingSymbol
            : $"{symbol}{IstClock.Now:yyMMM}{(int)atmStrike}{optionType}".ToUpper();


        // ── Build Signal using SignalBuilder ──────────────────────────────────
        // Buy()/Sell() MUST be called before WithIndexStops() — stops depend on direction.
        var isExpiryDay = _instrumentCache.IsExpiryDay(symbol);
        var risk = _atrRisk.CalculateIndexRisk(symbol, ltp, atrRaw, call, Settings);
        decimal sl = risk.StopLoss;
        decimal t1 = risk.Target1;
        decimal t2 = risk.Target2;

        // ===============================
        // OPTION Targets — option's OWN candles (ATR + support/resistance) first;
        // falls back to a delta-adjusted index-based estimate only when there isn't
        // enough option-candle history yet (e.g. just after open, or a strike picked
        // for the first time today).
        // ===============================

        decimal optionSL;
        decimal optionT1;
        decimal optionT2;

        var delta = Math.Abs(chainLeg!.Delta);
        if (delta <= 0)
            delta = 0.45m;

        var stopDistance = Math.Abs(ltp - sl);

        var optionRisk = stopDistance * delta;
        var optionTarget1 = atrRaw * Settings.AtrTarget1Multiplier * delta;
        var optionTarget2 = atrRaw * Settings.AtrTarget2Multiplier * delta;

        optionSL = Math.Round(optionLtp - optionRisk, 2);

        var maxLoss = optionLtp * 0.80m;
        if (optionSL < maxLoss)
            optionSL = maxLoss;

        optionT1 = Math.Round(optionLtp + optionTarget1, 2);
        optionT2 = Math.Round(optionLtp + optionTarget2, 2);
        // ===============================
        // Build Signal
        // ===============================

        var builder = new SignalBuilder(symbol);

        if (call)
            builder.CALL();
        else
            builder.PUT();


        builder
            .AtIndexPrice(ltp)

            .WithIndexTargets(
                sl,
                t1,
                t2)

            .ForOption(
                atmStrike,
                optionType,
                optionSymbol,
                optionLtp,
                chainLeg.Delta)

            .WithOptionTargets(
                optionSL,
                optionT1,
                optionT2)

            .OnExchange(
                _instrumentCache.GetExchange(symbol))

            // Quantity is not decided here. AdaptiveLotSizingService is the single authority and
            // overwrites Signal.Quantity in StrategyManager immediately after this returns, using
            // a fixed 1 lot. The old confidence-scaled LotsForConfidence (1/2/3 lots) and its
            // high-vega decrement were both removed: the value never survived to the broker, and
            // with the volume bonus gone the confidence ceiling is 85, making its >90 tier
            // unreachable and its >80 tier misleading.
            .WithLots(1)

            .WithConfidence(confidence);


        return SignalGenerationResult.Success(builder.Build());
    }
}
