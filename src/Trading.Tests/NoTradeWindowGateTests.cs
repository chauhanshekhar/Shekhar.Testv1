using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// NoTradeWindowGate: no new entries inside a configured IST window (11:00-12:30 by default values).
/// The decision is a pure function of (minute of day, start, end); the wiring tests use a window that
/// covers the whole day so they do not depend on the wall clock.
/// </summary>
public class NoTradeWindowGateTests
{
    private const int Start = NoTradeWindowGate.DefaultStartMinute;   // 11:00
    private const int End = NoTradeWindowGate.DefaultEndMinute;       // 12:30

    [Theory]
    [InlineData(10 * 60 + 59, false)]   // 10:59 - before
    [InlineData(11 * 60, true)]         // 11:00 - start is inclusive
    [InlineData(11 * 60 + 43, true)]    // 11:43 - signal 107's entry
    [InlineData(12 * 60 + 29, true)]    // 12:29
    [InlineData(12 * 60 + 30, false)]   // 12:30 - end is exclusive
    [InlineData(13 * 60 + 8, false)]    // 13:08 - after
    public void Window_IsStartInclusive_EndExclusive(int minuteOfDay, bool blocked)
        => Assert.Equal(blocked, NoTradeWindowGate.Evaluate(minuteOfDay, Start, End) is not null);

    [Fact]
    public void ReasonNamesTheWindowAndTheTime()
    {
        var reason = NoTradeWindowGate.Evaluate(11 * 60 + 5, Start, End);

        Assert.NotNull(reason);
        Assert.Contains("11:00-12:30", reason);
        Assert.Contains("11:05", reason);
    }

    [Theory]
    [InlineData(750, 660)]   // end before start
    [InlineData(660, 660)]   // empty window
    public void EmptyOrInvertedWindow_NeverBlocks(int start, int end)
        => Assert.Null(NoTradeWindowGate.Evaluate(700, start, end));

    [Theory]
    [InlineData(NoTradeWindowGate.SideBoth, "CALL", true)]
    [InlineData(NoTradeWindowGate.SideBoth, "PUT", true)]
    [InlineData(NoTradeWindowGate.SideCallOnly, "CALL", true)]
    [InlineData(NoTradeWindowGate.SideCallOnly, "PUT", false)]
    [InlineData(NoTradeWindowGate.SidePutOnly, "PUT", true)]
    [InlineData(NoTradeWindowGate.SidePutOnly, "CALL", false)]
    public void Side_ScopesTheWindowToOneDirection(int side, string signalType, bool applies)
        => Assert.Equal(applies, NoTradeWindowGate.AppliesTo(side, signalType));

    [Fact]
    public async Task CallOnlyWindow_BlocksCall_NotPut()
    {
        var settings = AllDay().Set(NoTradeWindowGate.SideKey, NoTradeWindowGate.SideCallOnly);

        var call = await Block(settings, TradingConst.SignalType.CALL);
        Assert.NotNull(call);
        Assert.Contains("CALL", call);
        Assert.Null(await Block(settings, TradingConst.SignalType.PUT));
    }

    [Fact]
    public async Task DefaultSide_BlocksBothDirections()
    {
        Assert.NotNull(await Block(AllDay(), TradingConst.SignalType.CALL));
        Assert.NotNull(await Block(AllDay(), TradingConst.SignalType.PUT));
    }

    [Fact]
    public async Task DisabledByDefault_Proceeds()
        => Assert.Null(await Block(new FakeStrategySettingsService()
            .Set(NoTradeWindowGate.StartKey, 0).Set(NoTradeWindowGate.EndKey, 1440)));

    private static FakeStrategySettingsService AllDay() => new FakeStrategySettingsService()
        .Set(NoTradeWindowGate.EnableKey, 1).Set(NoTradeWindowGate.StartKey, 0).Set(NoTradeWindowGate.EndKey, 1440);

    [Fact]
    public async Task Enabled_WithAllDayWindow_Blocks()
    {
        var reason = await Block(new FakeStrategySettingsService()
            .Set(NoTradeWindowGate.EnableKey, 1).Set(NoTradeWindowGate.StartKey, 0).Set(NoTradeWindowGate.EndKey, 1440));

        Assert.NotNull(reason);
        Assert.Contains("No-trade window", reason);
        Assert.Contains("(BANKNIFTY)", reason);
    }

    [Fact]
    public async Task Enabled_WithEmptyWindow_Proceeds()
        => Assert.Null(await Block(new FakeStrategySettingsService()
            .Set(NoTradeWindowGate.EnableKey, 1).Set(NoTradeWindowGate.StartKey, 700).Set(NoTradeWindowGate.EndKey, 700)));

    private static Task<string?> Block(FakeStrategySettingsService settings, string signalType = TradingConst.SignalType.PUT) =>
        new NoTradeWindowGate().BlockAsync(Context(settings), null!, new Signal { Symbol = "BANKNIFTY", SignalType = signalType },
            CancellationToken.None);

    private static SignalContext Context(FakeStrategySettingsService settings) => new(
        Symbol: "BANKNIFTY",
        CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
        SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
        Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
        HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
        Settings: new TradingSettings(), EventCalendar: null!, Instruments: null!,
        SystemSettings: null!, RuntimeSettings: null!, StrategySettings: settings);
}
