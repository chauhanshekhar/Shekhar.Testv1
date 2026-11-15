using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// ConsecutiveLossGate is now switchable from StrategySettings (per symbol, live and paper apart),
/// so trading after the 3-loss stop no longer needs TradeJournal losses edited into profits.
/// Defaults must reproduce the old behaviour: 3 losses, both modes.
/// </summary>
public class ConsecutiveLossGateTests
{
    private const string Symbol = "SENSEX";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Defaults_AreThreeInBothModes(bool isPaper)
        => Assert.Equal(3, ConsecutiveLossGate.ResolveLimit(new FakeStrategySettingsService(), Symbol, 3, isPaper));

    [Fact]
    public void PaperSwitchOff_TurnsTheGateOffInPaperOnly()
    {
        var s = new FakeStrategySettingsService().Set("EnableConsecutiveLossGatePaper", 0);

        Assert.Equal(0, ConsecutiveLossGate.ResolveLimit(s, Symbol, 3, isPaper: true));
        Assert.Equal(3, ConsecutiveLossGate.ResolveLimit(s, Symbol, 3, isPaper: false));   // live untouched
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MasterSwitchOff_TurnsTheGateOffEverywhere(bool isPaper)
        => Assert.Equal(0, ConsecutiveLossGate.ResolveLimit(
            new FakeStrategySettingsService().Set("EnableConsecutiveLossGate", 0), Symbol, 3, isPaper));

    [Fact]
    public void LimitIsConfigurable()
        => Assert.Equal(5, ConsecutiveLossGate.ResolveLimit(
            new FakeStrategySettingsService().Set("MaxConsecutiveLosses", 5), Symbol, 3, isPaper: false));

    [Fact]
    public void NegativeLimit_IsTreatedAsOff()
        => Assert.Equal(0, ConsecutiveLossGate.ResolveLimit(
            new FakeStrategySettingsService().Set("MaxConsecutiveLosses", -1), Symbol, 3, isPaper: false));

    [Fact]
    public async Task GateOff_NeverReadsTheJournal()
    {
        // JournalRepo is null: if the gate touched it with the switch off, this would throw.
        var ctx = new SignalContext(
            Symbol: Symbol,
            CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
            SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
            Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
            HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
            Settings: new TradingSettings(), EventCalendar: null!, Instruments: null!,
            SystemSettings: null!, RuntimeSettings: null!,
            StrategySettings: new FakeStrategySettingsService().Set("EnableConsecutiveLossGate", 0));

        Assert.Null(await new ConsecutiveLossGate().BlockAsync(ctx, null!, CancellationToken.None));
    }
}
