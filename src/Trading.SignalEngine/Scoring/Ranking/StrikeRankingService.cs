using Trading.SignalEngine.Scoring.Models;

namespace Trading.SignalEngine.Scoring.Ranking;

/// <summary>Ranks each side by TotalScore and decides BuyCe / BuyPe / NoTrade.</summary>
public class StrikeRankingService
{
    private const decimal BuyThreshold = 85m;
    private const decimal OppositeSideCap = 50m;

    public HeroZeroResult Rank(List<StrikeScore> ceScores, List<StrikeScore> peScores)
    {
        var ceRanking = ceScores.OrderByDescending(s => s.TotalScore).ToList();
        var peRanking = peScores.OrderByDescending(s => s.TotalScore).ToList();

        var heroCe = ceRanking.FirstOrDefault();
        var heroPe = peRanking.FirstOrDefault();

        var ceTotal = heroCe?.TotalScore ?? 0m;
        var peTotal = heroPe?.TotalScore ?? 0m;

        HeroZeroDecision decision;
        string reason;

        if (heroCe != null && ceTotal >= BuyThreshold && peTotal <= OppositeSideCap)
        {
            decision = HeroZeroDecision.BuyCe;
            reason = $"CE Hero {ceTotal:F0} >= {BuyThreshold:F0} and PE Hero {peTotal:F0} <= {OppositeSideCap:F0}";
        }
        else if (heroPe != null && peTotal >= BuyThreshold && ceTotal <= OppositeSideCap)
        {
            decision = HeroZeroDecision.BuyPe;
            reason = $"PE Hero {peTotal:F0} >= {BuyThreshold:F0} and CE Hero {ceTotal:F0} <= {OppositeSideCap:F0}";
        }
        else
        {
            decision = HeroZeroDecision.NoTrade;
            reason = $"No dominant side — CE Hero {ceTotal:F0}, PE Hero {peTotal:F0}";
        }

        return new HeroZeroResult
        {
            HeroCe = heroCe,
            HeroPe = heroPe,
            CeRanking = ceRanking,
            PeRanking = peRanking,
            Decision = decision,
            Reason = reason
        };
    }
}
