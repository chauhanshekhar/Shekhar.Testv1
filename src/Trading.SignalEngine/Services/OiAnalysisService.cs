namespace Trading.SignalEngine.Services;

public class OiAnalysisService
{
    public bool IsLongBuildUp(
        long oiChange,
        decimal priceChange)
    {
        return oiChange > 0 &&
               priceChange > 0;
    }

    public bool IsShortBuildUp(
        long oiChange,
        decimal priceChange)
    {
        return oiChange > 0 &&
               priceChange < 0;
    }

    public bool IsShortCovering(
        long oiChange,
        decimal priceChange)
    {
        return oiChange < 0 &&
               priceChange > 0;
    }

    public bool IsLongUnwinding(
        long oiChange,
        decimal priceChange)
    {
        return oiChange < 0 &&
               priceChange < 0;
    }

    public string GetMarketStructure(
        long oiChange,
        decimal priceChange)
    {
        if (oiChange > 0 && priceChange > 0)
            return "LONG_BUILDUP";

        if (oiChange > 0 && priceChange < 0)
            return "SHORT_BUILDUP";

        if (oiChange < 0 && priceChange > 0)
            return "SHORT_COVERING";

        if (oiChange < 0 && priceChange < 0)
            return "LONG_UNWINDING";

        return "NEUTRAL";
    }
}