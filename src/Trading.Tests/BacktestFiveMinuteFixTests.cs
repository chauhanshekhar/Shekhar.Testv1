using Trading.Backtest.Configurations;
using Trading.Backtest.Settings;
using Trading.Backtest.Simulation;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Xunit;

namespace Trading.Tests;

/// <summary>The replay reads EnableAtr5mStop / Target2RMultiple from StrategySettings rows, like live; no row = unchanged behaviour.</summary>
public class BacktestFiveMinuteFixTests
{
    private static readonly Dictionary<string, double> None = new();
    private static readonly Dictionary<string, int> Lots = new();

    private static ExitModel Model(params (string Key, decimal Value, bool Active)[] rows)
    {
        var settings = BacktestSettings.FromRows(rows.Select(r => new StrategySetting
        {
            SettingKey = r.Key, Symbol = null, SettingValue = r.Value, IsActive = r.Active,
        }));
        return ExitModel.From(settings, new TradingSettings(), None, None, Lots);
    }

    [Fact]
    public void Atr5mStop_off_keeps_the_1m_atr_stop()
    {
        var model = Model();
        Assert.Equal(model.IndexStopPoints(20.0), model.IndexStopPoints(20.0, 50.0, useAtr5m: false, atr5mMult: 1.0));
    }

    [Fact]
    public void Atr5mStop_on_uses_5m_atr_times_multiplier_then_clamps()
    {
        var model = Model();
        // 5m ATR 40 x 1.0 = 40 pts, inside the 20..100 clamp; the 1m ATR (10 -> 15 pts, floored to 20) is ignored.
        Assert.Equal(40.0, model.IndexStopPoints(10.0, 40.0, useAtr5m: true, atr5mMult: 1.0), 6);
        // Zero 5m ATR falls back to the 1m ATR like live.
        Assert.Equal(model.IndexStopPoints(10.0), model.IndexStopPoints(10.0, 0.0, useAtr5m: true, atr5mMult: 1.0), 6);
    }

    [Fact]
    public void Target2R_row_is_read_only_when_active_and_above_T1()
    {
        Assert.Null(Model().Symbols["BANKNIFTY"].Target2R);
        Assert.Equal(2.0, Model(("Target2RMultiple", 2m, true)).Symbols["BANKNIFTY"].Target2R);
        Assert.Null(Model(("Target2RMultiple", 2m, false)).Symbols["BANKNIFTY"].Target2R);
        Assert.Null(Model(("Target2RMultiple", 0.5m, true)).Symbols["BANKNIFTY"].Target2R);   // not above T1R
    }

    [Fact]
    public void Atr5m_flags_come_from_the_rows()
    {
        var cfg = Model(("EnableAtr5mStop", 1m, true), ("Atr5mStopMultiplier", 1.25m, true)).Symbols["NIFTY"];
        Assert.True(cfg.Atr5mStop);
        Assert.Equal(1.25, cfg.Atr5mMult);
    }

    [Fact]
    public void Research_T2_multiplier_overrides_the_Target2R_row()
    {
        var model = Model(("Target2RMultiple", 2m, true));
        var applied = new ExitGeometry(T2Mult: 3.0).ApplyTo(model);
        Assert.Null(applied.Symbols["SENSEX"].Target2R);
        Assert.Equal(2.0, new ExitGeometry(StopMult: 1.0).ApplyTo(model).Symbols["SENSEX"].Target2R);
    }
}
