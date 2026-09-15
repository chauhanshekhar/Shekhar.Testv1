namespace Trading.Infrastructure;

public class TradingSettings
{
    // =========================
    // Trading / Capital
    // =========================

    public decimal Capital { get; set; } = 100_000m;

    public decimal RiskPercent { get; set; } = 2m;

    public int MaxTradesPerDay { get; set; } = 10;

    public decimal MaxLossPerDay { get; set; } = 5000m;

    public decimal MaxLossPercent { get; set; } = 2m;

    public int MaxConsecLosses { get; set; } = 3;


    // =========================
    // Risk
    // =========================

    public int MaxLotSize { get; set; } = 5;

    public decimal MinRiskRewardRatio { get; set; } = 1.5m;

    public decimal AtrSlMultiplier { get; set; } = 1.5m;

    public decimal HighVegaThreshold { get; set; } = 2m;


    // =========================
    // Liquidity
    // =========================

    public Dictionary<string, long> LiquidityMinVolume { get; set; } = new()
    {
        ["NIFTY"] = 200000,
        ["BANKNIFTY"] = 15000,
        ["SENSEX"] = 500000,
        ["FINNIFTY"] = 50000,
        ["MIDCAPNIFTY"] = 25000
    };


    public Dictionary<string, long> LiquidityMinOI { get; set; } = new()
    {
        ["NIFTY"] = 20000,
        ["BANKNIFTY"] = 3000,
        ["SENSEX"] = 20000,
        ["FINNIFTY"] = 10000,
        ["MIDCAPNIFTY"] = 5000
    };


    public Dictionary<string, decimal> LiquidityMinLtp { get; set; } = new()
    {
        ["NIFTY"] = 20,
        ["BANKNIFTY"] = 50,
        ["SENSEX"] = 30,
        ["FINNIFTY"] = 20,
        ["MIDCAPNIFTY"] = 15
    };


    public Dictionary<string, decimal> LiquidityMaxLtp { get; set; } = new()
    {
        ["NIFTY"] = 300,
        ["BANKNIFTY"] = 1200,
        ["SENSEX"] = 700,
        ["FINNIFTY"] = 300,
        ["MIDCAPNIFTY"] = 200
    };


    // =========================
    // Hero Zero
    // =========================

    public decimal HeroZeroMinScore { get; set; } = 90m;

    public TimeSpan HeroZeroStartTime { get; set; }
        = new(14, 0, 0);


    public decimal HeroZeroMinPremium { get; set; } = 5m;


    public Dictionary<string, decimal> HeroZeroMaxPremium { get; set; } = new()
    {
        ["NIFTY"] = 30,
        ["BANKNIFTY"] = 50,
        ["SENSEX"] = 60,
        ["FINNIFTY"] = 25,
        ["MIDCAPNIFTY"] = 20
    };


    public decimal HeroZeroSLPercent { get; set; } = 50;

    public decimal HeroZeroT1Multiplier { get; set; } = 2;

    public decimal HeroZeroT2Multiplier { get; set; } = 5;


    // =========================
    // =========================
    // Indicator snapshot outcome tracking (+20 / -20 INDEX points)
    // =========================
    // ANALYTICS ONLY — never read by signal generation, order placement or exit management.
    // Default OFF so deploying the code starts no work against the database; the historical
    // backfill only begins once this is explicitly switched on.
    public bool EnableOutcomeTracking { get; set; } = false;

    // Snapshots processed per pass. Each pass issues a fixed number of queries regardless of this
    // value, so it trades memory for fewer round trips, not more of them.
    public int OutcomeTrackingBatchSize { get; set; } = 2000;

    // Gap between passes while a backlog is still draining, and while it is caught up.
    public int OutcomeTrackingIntervalSeconds { get; set; } = 60;

    // Oldest snapshot the backfill will reach back to. Guards against an unbounded first run on a
    // table that has been accumulating for months.
    public int OutcomeTrackingBackfillDays { get; set; } = 30;


    // Expiry-day routing
    // =========================
    // The dedicated Expiry ENGINE is gone — expiry days now route to Directional (before the
    // gamma-blast boundary) or HeroZero (at/after), which is what StrategySelectorService has
    // always actually returned. Only the two time boundaries that routing reads survive here;
    // the engine's own SL/target/score knobs were removed with it, so no one can tune a value
    // that silently affects nothing.

    // First-30-min-of-expiry-day no-trade window — wide spreads, gap traps, IV still settling,
    // fake breakouts. Read by StrategySelectorService's expiry dispatch.
    public TimeSpan ExpiryMorningNoTradeEnd { get; set; }
        = new(9, 30, 0);


    // Directional -> HeroZero handover on expiry day.
    public TimeSpan ExpiryGammaBlastStartTime { get; set; }
        = new(14, 0, 0);


    // =========================
    // Score Weights
    // =========================

    public decimal ScoreTrendWeight { get; set; } = 20;

    public decimal ScoreOIWeight { get; set; } = 20;

    public decimal ScoreVolumeWeight { get; set; } = 15;

    public decimal ScoreGreeksWeight { get; set; } = 20;

    public decimal ScoreLiquidityWeight { get; set; } = 10;

    public decimal ScoreMomentumWeight { get; set; } = 15;


    // =========================
    // Spread Gate
    // =========================

    public decimal SpreadLowPremiumMaxPct { get; set; } = 5;

    public decimal SpreadMediumPremiumMaxPct { get; set; } = 3;

    public decimal SpreadHighPremiumMaxPct { get; set; } = 2;


    // =========================
    // ATR Risk Management
    // =========================
    // Single, unconditional path for all three engines (Directional, Expiry, HeroZero) —
    // no on/off switch. There's no fixed-points fallback left to switch back to.

    public decimal AtrStopLossMultiplier { get; set; } = 1.5m;

    public decimal AtrTarget1Multiplier { get; set; } = 2.0m;

    public decimal AtrTarget2Multiplier { get; set; } = 3.0m;

    public decimal MinimumIndexStopPoints { get; set; } = 20m;

    public decimal MaximumIndexStopPoints { get; set; } = 100m;

    // Caps how much ExitWorker's ATR-based option trailing SL can move per evaluation,
    // as a fraction of the ATR distance itself — scales with premium/volatility instead of
    // a fixed rupee step. 0.30 conservative, 0.50 recommended, 0.75 aggressive, 1.00 = no cap.
    public decimal TrailingSLStepFraction { get; set; } = 0.50m;

    // Below this option premium, ExitWorker skips ATR-based trailing entirely for that
    // signal's strategy — the ATR×delta distance can dwarf the premium itself at that scale.
    // Keyed by Signal.SelectedStrategy ("HeroZero"/"Directional"/"Expiry"), since strategies
    // run at very different premium scales (HeroZero deliberately trades cheap far-OTM
    // premiums; Expiry's GammaBlast mode can too). Other exit paths (OptionSlHit/
    // HeroZero-safety/ForceExitEod/index SL) are unaffected regardless of this threshold.
    public Dictionary<string, decimal> MinPremiumForAtrTrailing { get; set; } = new()
    {
        ["HeroZero"] = 5m,
        ["Directional"] = 5m,
        ["Expiry"] = 2m,
    };


    // =========================
    // Signal Engine
    // =========================

    public decimal MinBodyPercent { get; set; } = 40m;

    public Dictionary<string, decimal> MinAtrPercent { get; set; } = new()
    {
        ["NIFTY"] = 0.04m,
        ["BANKNIFTY"] = 0.05m,
        ["SENSEX"] = 0.04m,
        ["FINNIFTY"] = 0.10m,
        ["MIDCAPNIFTY"] = 0.10m
    };


    // =========================
    // Trailing SL
    // =========================

    public decimal TrailSLAt50PctProfit { get; set; } = 0.90m;

    public decimal TrailSLAt30PctProfit { get; set; } = 0.85m;

    public decimal TrailSLDefault { get; set; } = 0.80m;

    public decimal TrailSLActivationPct { get; set; } = 15;


    // =========================
    // Adaptive Lot Size
    // =========================

    public decimal ConfidenceLot1Min { get; set; } = 70m;   // 70-79 confidence -> 1 lot

    public decimal ConfidenceLot2Min { get; set; } = 80m;   // 80-89 -> 2 lots

    public decimal ConfidenceLot3Min { get; set; } = 90m;   // 90+   -> 3 lots

    public int AdaptiveLotMin { get; set; } = 1;

    // Distinct from MaxConsecLosses above (the hard-stop count) — this is a lighter,
    // earlier tier that reduces size before the hard stop kicks in.
    public int AdaptiveLotConsecutiveLossCount { get; set; } = 2;

    // Percentage-scale (matches MinAtrPercent's unit) — compared against market.AtrPct, not AtrRaw.
    public Dictionary<string, decimal> AdaptiveLotHighAtrThreshold { get; set; } = new()
    {
        ["NIFTY"] = 0.10m,
        ["BANKNIFTY"] = 0.22m,
        ["SENSEX"] = 0.20m,
        ["FINNIFTY"] = 0.25m,
        ["MIDCAPNIFTY"] = 0.30m
    };


    // =========================
    // Intelligent Strategy Selection
    // =========================

    // Never weight a StrategyPerformance bucket's win rate until it has this many trades — guards
    // against overfitting to a handful of trades (100, not 20-30 — optimizing to randomness is worse
    // than no historical weighting at all).
    public int MinTradesForHistoricalWeight { get; set; } = 100;


    // =========================
    // Market Classification
    // =========================

    public decimal StrongTrendEmaGapPct { get; set; } = 0.15m;
    public decimal TrendEmaGapPct { get; set; } = 0.05m;
    public decimal RsiBullConfirm { get; set; } = 58m;
    public decimal RsiBearConfirm { get; set; } = 42m;
    public decimal BreakoutVolumeRatio { get; set; } = 1.5m;
    public decimal BreakoutPriceChangeFloorPct { get; set; } = 0.10m;


    // =========================
    // Broker Stop Loss (SL_M)
    // =========================

    // How long ExitWorker tolerates having no resting broker SL_M order (placement kept
    // failing) before falling back to a MARKET exit as a last resort. Under normal conditions
    // this never triggers — the broker order is what protects the position. Kept short (5s,
    // not 12s) since a slow fallback during expiry-day volatility is itself a risk.
    public int StopLossFallbackTimeoutSeconds { get; set; } = 5;

    // Caps consecutive SL_M placement attempts per signal — after this many failures,
    // StopLossOrderCoordinator stops retrying (avoids hammering a broker that's down) and
    // leaves protection to the MARKET fallback above once price actually crosses OptionSL.
    public int MaxStopLossRetries { get; set; } = 5;
}