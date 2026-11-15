using Trading.Worker;
using Xunit;

namespace Trading.Tests;

/// <summary>Full-chain PCR = total PE OI / total CE OI across every strike (stored as PCR_FULL_CHAIN).</summary>
public class FullChainPcrTests
{
    [Fact]
    public void SumsAllStrikes_AndRoundsToFourDecimals()
    {
        var pcr = OptionChainWorker.CalculateFullChainPcr(new (long?, long?)[] { (100, 150), (200, 100), (null, 50) });

        Assert.Equal(Math.Round(300m / 300m, 4), pcr);
    }

    [Fact]
    public void MissingLegsCountAsZero()
    {
        var pcr = OptionChainWorker.CalculateFullChainPcr(new (long?, long?)[] { (300, null), (null, 150) });

        Assert.Equal(0.5m, pcr);
    }

    [Fact]
    public void NoCeOi_ReturnsNull_NotZero()
    {
        Assert.Null(OptionChainWorker.CalculateFullChainPcr(new (long?, long?)[] { (null, 500), (0, 100) }));
    }
}
