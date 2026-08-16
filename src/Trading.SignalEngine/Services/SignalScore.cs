namespace Trading.SignalEngine.Services;

/// <summary>
/// Per-direction (CALL or PUT) score breakdown — used only when EnableSignalScoring is on for a
/// symbol (see SignalEngineService.GenerateScored). Each component is independent so a weak
/// secondary factor (body%, ATR, PCR, OI) reduces the total instead of hard-blocking the setup;
/// the primary directional filters (Trend/EMA20/EMA50/RSI) still carry the most weight.
/// </summary>
public sealed class SignalScore
{
    public bool IsCall { get; set; }

    public int TrendScore { get; set; }
    public int Ema20Score { get; set; }
    public int Ema50Score { get; set; }
    public int RsiScore { get; set; }
    public int PriceChangeScore { get; set; }
    public int CandleScore { get; set; }
    public int BodyScore { get; set; }
    public int AtrScore { get; set; }
    public int PcrScore { get; set; }
    public int OiScore { get; set; }
    public int ConfidenceScore { get; set; }

    public int Total =>
        TrendScore + Ema20Score + Ema50Score + RsiScore + PriceChangeScore +
        CandleScore + BodyScore + AtrScore + PcrScore + OiScore + ConfidenceScore;

    // Confidence % (existing ComputeConfidence output for this direction) — carried alongside the
    // score purely for the audit breakdown and the tie-break between two eligible candidates.
    public decimal Confidence { get; set; }

    public override string ToString() =>
        $"{(IsCall ? "CALL" : "PUT")} Score={Total} " +
        $"Trend={TrendScore} EMA20={Ema20Score} EMA50={Ema50Score} RSI={RsiScore} " +
        $"PriceChange={PriceChangeScore} Candle={CandleScore} Body={BodyScore} " +
        $"ATR={AtrScore} PCR={PcrScore} OI={OiScore} Confidence={ConfidenceScore}";
}
