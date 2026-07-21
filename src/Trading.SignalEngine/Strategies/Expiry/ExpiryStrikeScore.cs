namespace Trading.SignalEngine.Strategies.Expiry;

public class ExpiryStrikeScore
{
    public string Symbol { get; set; } = "";
    public string TradingSymbol { get; set; } = "";
    public decimal StrikePrice { get; set; }
    public string OptionType { get; set; } = "";
    public decimal Ltp { get; set; }
    public decimal Delta { get; set; }

    public decimal GammaScore { get; set; }
    public decimal VolumeScore { get; set; }
    public decimal OiShiftScore { get; set; }
    public decimal PremiumMomentumScore { get; set; }
    public decimal TrendScore { get; set; }

    // ── Option Score additions ──────────────────────────────────────────────
    public decimal GreeksScore { get; set; }
    public decimal LiquidityScore { get; set; }

    // ── Market Score — index-level, same value for every strike of this side this cycle ────
    public decimal EmaTrendScore { get; set; }
    public decimal Ema5TrendScore { get; set; }
    public decimal RsiScore { get; set; }
    public decimal PcrScore { get; set; }
    public decimal AtrScore { get; set; }
    public decimal MarketVolumeScore { get; set; }
    public decimal PriceChangeScore { get; set; }

    // Raw sum, max 130 (Trend10+Volume25+Gamma25+OiShift25+Momentum15+Greeks20+Liquidity10).
    private decimal OptionScoreRaw =>
        GammaScore + VolumeScore + OiShiftScore + PremiumMomentumScore + TrendScore + GreeksScore + LiquidityScore;

    // Raw sum, max 70 (7 components x 10) — see ExpiryMarketScoreService.RawMax.
    private decimal MarketScoreRaw =>
        EmaTrendScore + Ema5TrendScore + RsiScore + PcrScore + AtrScore + MarketVolumeScore + PriceChangeScore;

    private const decimal OptionScoreRawMax = 130m;

    /// <summary>Rescaled 0-100 — ranks eligible contracts only; never overrides the MarketScore gate.</summary>
    public decimal OptionScore => OptionScoreRaw / OptionScoreRawMax * 100m;

    /// <summary>Rescaled 0-100 — same value for every strike on this side this cycle.</summary>
    public decimal MarketScore => MarketScoreRaw / ExpiryMarketScoreService.RawMax * 100m;

    /// <summary>
    /// Weighted confidence, NOT an additive sum: MarketScore(60%) + OptionScore(40%). A poor market
    /// score can never be outweighed by a high option score — it caps the ceiling, it doesn't just
    /// average it away. The hard pass/fail gate itself (reject before option scoring at all) lives in
    /// ExpirySignalEngine, using this same MarketScore on both sides before the option universe is built.
    /// </summary>
    public decimal TotalScore => (MarketScore * 0.60m) + (OptionScore * 0.40m);
}
