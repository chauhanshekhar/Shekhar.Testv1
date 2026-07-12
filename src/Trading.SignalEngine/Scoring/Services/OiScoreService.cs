using Trading.SignalEngine.Services;

namespace Trading.SignalEngine.Scoring.Services;

/// <summary>
/// Max 20 — delegates structure classification to the existing OiAnalysisService.
/// For PE strikes the underlying price change is inverted before classification,
/// same convention already used by OiConfirmationGate (falling underlying = PE-favorable).
/// </summary>
public class OiScoreService
{
    private readonly OiAnalysisService _oi;

    public OiScoreService(OiAnalysisService oi)
    {
        _oi = oi;
    }

    public decimal Score(long changeInOi, decimal underlyingPriceChangePct, bool isCall)
    {
        var favorableChange = isCall ? underlyingPriceChangePct : -underlyingPriceChangePct;

        var structure = _oi.GetMarketStructure(changeInOi, favorableChange);

        return structure switch
        {
            "LONG_BUILDUP" => 20m,
            "SHORT_COVERING" => 15m,
            "NEUTRAL" => 5m,
            _ => 0m // SHORT_BUILDUP / LONG_UNWINDING against this option's own side
        };
    }
}
