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
    // Expiry Engine
    // =========================

    public TimeSpan ExpiryMorningEndTime { get; set; }
        = new(11, 0, 0);


    public TimeSpan ExpiryGammaBlastStartTime { get; set; }
        = new(14, 0, 0);


    public decimal ExpiryGammaBlastMinScore { get; set; } = 90;


    public decimal ExpiryMorningSLPercent { get; set; } = 25;

    public decimal ExpiryMorningT1Multiplier { get; set; } = 1.3m;

    public decimal ExpiryMorningT2Multiplier { get; set; } = 1.5m;


    public decimal ExpiryNormalSLPercent { get; set; } = 35;

    public decimal ExpiryNormalT1Multiplier { get; set; } = 1.5m;

    public decimal ExpiryNormalT2Multiplier { get; set; } = 2m;


    public decimal ExpiryGammaSLPercent { get; set; } = 50;

    public decimal ExpiryGammaT1Multiplier { get; set; } = 2;

    public decimal ExpiryGammaT2Multiplier { get; set; } = 5;


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
    // Signal Engine
    // =========================

    public decimal MinBodyPercent { get; set; } = 40m;

    public Dictionary<string, decimal> MinAtrPercent { get; set; } = new()
    {
        ["NIFTY"] = 0.10m,
        ["BANKNIFTY"] = 0.15m,
        ["SENSEX"] = 0.15m,
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
}