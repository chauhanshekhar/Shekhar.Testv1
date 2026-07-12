namespace Trading.SignalEngine.Scoring.Services;

/// <summary>
/// Max 15 — cross-sectional, split 50/50 between two robust-to-outlier measures
/// across same-type strikes in the current chain snapshot:
///   - ratio vs the group MEDIAN volume (median, not mean, so one huge strike
///     can't drag the whole side's baseline up and make good strikes look weak)
///   - the strike's own percentile rank within the group
/// </summary>
public class VolumeScoreService
{
    public decimal Score(long strikeVolume, decimal medianVolumeForType, decimal percentileRank, decimal maxScore = 15m)
    {
        var scale = maxScore / 15m;

        decimal ratioScore = 0m;
        if (medianVolumeForType > 0)
        {
            var ratio = strikeVolume / medianVolumeForType;
            ratioScore =
                ratio > 3m ? 15m :
                ratio > 2m ? 10m :
                ratio > 1m ? 5m : 0m;
        }

        var percentileScore =
            percentileRank >= 0.90m ? 15m :
            percentileRank >= 0.75m ? 10m :
            percentileRank >= 0.50m ? 5m : 0m;

        return Math.Round(((ratioScore * 0.5m) + (percentileScore * 0.5m)) * scale, 2);
    }
}
