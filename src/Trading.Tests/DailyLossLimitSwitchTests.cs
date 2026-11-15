using Trading.Infrastructure;
using Trading.Tests.Fakes;
using Trading.Worker;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The daily-loss limit inside AdaptiveLotSizingService (Capital x MaxLossPercent) was the only daily-loss
/// stop running on 2026-10-01 - CircuitBreakerGate is not registered - and it blocked 3,130 signal cycles
/// from 09:52. EnableDailyLossLimit (default on) is its switch; 0 means no daily-loss stop.
/// </summary>
public class DailyLossLimitSwitchTests
{
    // Capital 2,00,000 x 2% = 4,000 budget (the 2026-10-01 production values).
    [Theory]
    [InlineData(4570, 0, 0, true)]         // losses 4,570 >= 4,000
    [InlineData(4000, 0, 0, true)]         // exactly at the budget - inclusive
    [InlineData(3999, 0, 0, false)]
    [InlineData(0, -3000, -1000, true)]    // net -3,000 and unrealised -1,000 = combined 4,000
    [InlineData(0, 500, -1000, false)]
    public void DailyLossReason_UsesTotalLossAndCombinedPnl(double totalLoss, double net, double unrealised, bool blocked)
        => Assert.Equal(blocked, AdaptiveLotSizingService.DailyLossReason(200_000m, 2m, (decimal)totalLoss, (decimal)net, (decimal)unrealised) is not null);

    [Fact]
    public void Reason_StatesTheBudget()
        => Assert.Contains("₹4000", AdaptiveLotSizingService.DailyLossReason(200_000m, 2m, 5000m, 0m, 0m));

    [Fact]
    public async Task SwitchedOff_NeverReadsPnl_AndReturnsFixedLots()
    {
        // PnlRepo is null: if the limit still ran, reading it would throw.
        var settings = new FakeStrategySettingsService().Set(AdaptiveLotSizingService.EnableDailyLossLimitKey, 0);

        var decision = await new AdaptiveLotSizingService().DetermineAsync(Context(settings), null!, 60m);

        Assert.Equal(4, decision.Lots);
    }

    private static SignalContext Context(FakeStrategySettingsService settings) => new(
        Symbol: "BANKNIFTY",
        CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
        SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
        Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
        HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
        Settings: new TradingSettings { AdaptiveLotMin = 4 }, EventCalendar: null!, Instruments: null!,
        SystemSettings: null!, RuntimeSettings: null!, StrategySettings: settings);
}
