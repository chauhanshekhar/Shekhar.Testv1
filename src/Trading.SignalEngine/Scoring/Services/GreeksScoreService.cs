namespace Trading.SignalEngine.Scoring.Services;

/// <summary>
/// Max 20 (Delta 10 + Gamma 5 + Theta 5). Gamma/Theta are judged against the
/// cross-sectional mean for same-type strikes — fixed bands don't hold across
/// symbols/moneyness, but "better than the rest of this chain right now" does.
/// </summary>
public class GreeksScoreService
{
    public decimal Score(
        decimal delta,
        decimal gamma,
        decimal theta,
        decimal averageGammaForType,
        decimal averageAbsThetaForType)
    {
        var absDelta = Math.Abs(delta);

        var deltaScore =
            absDelta >= 0.35m && absDelta <= 0.60m ? 10m :
            (absDelta >= 0.20m && absDelta < 0.35m) || (absDelta > 0.60m && absDelta <= 0.75m) ? 5m :
            0m;

        var gammaScore = gamma > averageGammaForType ? 5m : 0m;

        var absTheta = Math.Abs(theta);
        var thetaScore = absTheta < averageAbsThetaForType ? 5m : 0m;

        return deltaScore + gammaScore + thetaScore;
    }
}
