using Trading.Domain;
using Xunit;

namespace Trading.Tests;

/// <summary>An exit at an OptionSL that was trailed above entry is a profit exit: label it TRAILING_SL_HIT, not OPTION_SL_HIT.</summary>
public class TrailingSlLabelTests
{
    [Theory]
    [InlineData(155.30, 138.15, TradingConst.ExitReason.TrailingSlHit)]   // signal 165: stop trailed above entry
    [InlineData(138.20, 138.15, TradingConst.ExitReason.TrailingSlHit)]
    [InlineData(138.15, 138.15, TradingConst.ExitReason.OptionSlHit)]     // at entry: not above it
    [InlineData(110.00, 138.15, TradingConst.ExitReason.OptionSlHit)]     // initial stop below entry: a real stop-out
    [InlineData(155.30, 0, TradingConst.ExitReason.OptionSlHit)]          // unknown entry: keep the original label
    public void ForOptionStop_LabelsByStopVsEntry(double optionSl, double entry, string expected)
        => Assert.Equal(expected, TradingConst.ExitReason.ForOptionStop((decimal)optionSl, (decimal)entry));
}
