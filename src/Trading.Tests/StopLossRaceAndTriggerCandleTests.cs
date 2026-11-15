using Trading.Domain.Entities;
using Trading.Tests.Fakes;
using Trading.Worker;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Two real-money safety rules, pinned because both failures are silent.
///
/// 1. A dead SL_M's terminal state must never be written onto a signal that has since moved on to
///    a DIFFERENT, live stop — that erases the resting order's id and causes ExitWorker to place a
///    SECOND resting SELL against the same position.
///
/// 2. The entry trigger candle must not depend on WHICH real-money mode is active. HeroZero mode
///    is Live execution with the lot size forced to 1, so it must use the same trigger candle.
/// </summary>
public class StopLossRaceAndTriggerCandleTests
{
    // ── Dead SL_M reconciliation ────────────────────────────────────────────────────────────

    [Fact]
    public void DeadStop_IsAppliedWhenTheSignalTracksThatSameOrder()
    {
        Assert.True(BrokerOrderSyncWorker.ShouldApplyDeadStopLoss("GSMFO-AAA", "GSMFO-AAA"));
    }

    [Fact]
    public void DeadStop_IsAppliedWhenTheSignalTracksNoStopAtAll()
    {
        // Placement failed before any id was recorded — recording FAILED/CANCELLED is accurate
        // and there is nothing to clobber.
        Assert.True(BrokerOrderSyncWorker.ShouldApplyDeadStopLoss("", "GSMFO-AAA"));
        Assert.True(BrokerOrderSyncWorker.ShouldApplyDeadStopLoss(null, "GSMFO-AAA"));
    }

    [Fact]
    public void DeadStop_IsSkippedWhenANewerStopIsAlreadyResting()
    {
        // THE RACE: the sync worker loaded the old order row, then ExitWorker cancelled it and
        // placed GSMFO-BBB. Writing the old order's death here would erase BBB's id and trigger a
        // duplicate placement next cycle.
        Assert.False(BrokerOrderSyncWorker.ShouldApplyDeadStopLoss("GSMFO-BBB", "GSMFO-AAA"));
    }

    [Fact]
    public void DeadStop_IsSkippedWhenTheDeadOrderHasNoBrokerId()
    {
        // Cannot prove the two refer to the same order, so the resting stop is left alone.
        Assert.False(BrokerOrderSyncWorker.ShouldApplyDeadStopLoss("GSMFO-BBB", ""));
        Assert.False(BrokerOrderSyncWorker.ShouldApplyDeadStopLoss("GSMFO-BBB", null));
    }

    // ── Entry trigger candle vs trading mode ────────────────────────────────────────────────

    [Theory]
    [InlineData(TradingMode.Live, true)]        // real money
    [InlineData(TradingMode.HeroZero, true)]    // ALSO real money — same entry strategy, 1 lot
    [InlineData(TradingMode.Paper, false)]
    public void TriggerCandleDefault_FollowsRealMoneyNotLiveSpecifically(TradingMode mode, bool expectOneMinute)
    {
        var settings = new FakeStrategySettingsService();   // no override row

        Assert.Equal(expectOneMinute,
            StrategyManager.UseOneMinuteTrigger(mode, settings, "BANKNIFTY"));
    }

    [Fact]
    public void HeroZeroAndLive_AgreeOnTheTriggerCandle()
    {
        // The regression guarded against: testing `mode == Live` gave HeroZero a 5-minute trigger
        // while it placed real orders.
        var settings = new FakeStrategySettingsService();

        Assert.Equal(
            StrategyManager.UseOneMinuteTrigger(TradingMode.Live, settings, "BANKNIFTY"),
            StrategyManager.UseOneMinuteTrigger(TradingMode.HeroZero, settings, "BANKNIFTY"));
    }

    [Theory]
    [InlineData(TradingMode.Live)]
    [InlineData(TradingMode.HeroZero)]
    [InlineData(TradingMode.Paper)]
    public void ExplicitSetting_RemovesTheModeDependencyEntirely(TradingMode mode)
    {
        // What docs/set-one-minute-entry-trigger.sql configures: every mode uses the 1m candle.
        var settings = new FakeStrategySettingsService().Set("UseOneMinuteEntryCandle", 1m);

        Assert.True(StrategyManager.UseOneMinuteTrigger(mode, settings, "BANKNIFTY"));
    }

    [Theory]
    [InlineData(TradingMode.Live)]
    [InlineData(TradingMode.HeroZero)]
    public void ExplicitSettingCanAlsoForceFiveMinute(TradingMode mode)
    {
        var settings = new FakeStrategySettingsService().Set("UseOneMinuteEntryCandle", 0m);

        Assert.False(StrategyManager.UseOneMinuteTrigger(mode, settings, "BANKNIFTY"));
    }
}
