using Trading.Domain;
using Trading.Domain.Entities;

namespace Trading.SignalEngine.Services;

public class PcrService
{
    /// <summary>
    /// PCR = Put OI / Call OI
    /// </summary>
    public decimal Calculate(
        List<OptionChainData>? chain)
    {
        if (chain == null || chain.Count == 0)
            return 1m;

        long totalCeOi =
            chain
                .Where(x =>
                    string.Equals(
                        x.OptionType,
                        TradingConst.OptionType.CE,
                        StringComparison.OrdinalIgnoreCase))
                .Sum(x =>
                    x.OpenInterest);

        long totalPeOi =
            chain
                .Where(x =>
                    string.Equals(
                        x.OptionType,
                        TradingConst.OptionType.PE,
                        StringComparison.OrdinalIgnoreCase))
                .Sum(x =>
                    x.OpenInterest);

        if (totalCeOi <= 0)
            return 1m;

        return Math.Round(
            (decimal)totalPeOi / totalCeOi,
            4,
            MidpointRounding.AwayFromZero);
    }
}