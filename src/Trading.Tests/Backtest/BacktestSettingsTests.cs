using Microsoft.Extensions.Logging.Abstractions;
using Trading.Backtest.Configurations;
using Trading.Backtest.Entry;
using Trading.Backtest.Settings;
using Trading.Domain.Entities;
using Trading.Infrastructure.Services;
using Xunit;

namespace Trading.Tests.Backtest;

/// <summary>The replay must read settings exactly as production does, and a configuration must never leak into the live snapshot.</summary>
public class BacktestSettingsTests
{
    private static StrategySetting Row(string key, string? symbol, decimal value, bool active) =>
        new() { SettingKey = key, Symbol = symbol, SettingValue = value, IsActive = active };

    [Fact]
    public void ReadSemanticsMatchTheLiveStrategySettingsService()
    {
        var rows = new List<StrategySetting>
        {
            Row("A", null, 5, true), Row("A", "NIFTY", 7, false),     // inactive symbol row must not fall through to the global row
            Row("B", null, 1, false), Row("B", "SENSEX", 1, true),
            Row("C", "BANKNIFTY", 3, true), Row("D", null, 0, true),
        };
        using var live = new StrategySettingsService(null!, NullLogger<StrategySettingsService>.Instance);
        live.SetCacheForTest(StrategySettingsService.BuildCache(rows));
        var bt = BacktestSettings.FromRows(rows);

        foreach (var key in new[] { "A", "B", "C", "D", "MissingKey" })
        foreach (var symbol in new string?[] { null, "NIFTY", "BANKNIFTY", "SENSEX" })
        {
            Assert.Equal(live.IsSettingActive(key, symbol), bt.IsSettingActive(key, symbol));
            Assert.Equal(live.GetBool(key, symbol, true), bt.GetBool(key, symbol, true));
            Assert.Equal(live.GetBool(key, symbol, false), bt.GetBool(key, symbol, false));
            Assert.Equal(live.GetDecimal(key, symbol, 9m), bt.GetDecimal(key, symbol, 9m));
            Assert.Equal(live.GetInt(key, symbol, 9), bt.GetInt(key, symbol, 9));
        }
    }

    [Fact]
    public void MissingRowIsNotActiveButGettersFallBackToTheCallerDefault()
    {
        var s = new BacktestSettings();
        Assert.False(s.IsSettingActive("X", "NIFTY"));
        Assert.True(s.GetBool("X", "NIFTY", true));
        Assert.Equal(3m, s.GetDecimal("X", "NIFTY", 3m));
    }

    [Fact]
    public void ApplyingAConfigurationDoesNotChangeTheLiveSnapshot()
    {
        var live = BtSupport.LiveLike();
        var config = new BacktestConfiguration("CALL|T", TradeSide.Call, "test",
            [SettingChange.Set("CallRsiFloor", 50m), SettingChange.Off("CallRsiCeiling"), SettingChange.Set("EnableEmaGapFilter", 0m)]);

        var changed = config.Apply(live);

        Assert.Equal(45m, live.GetDecimal("CallRsiFloor", "NIFTY", 0));
        Assert.True(live.IsSettingActive("CallRsiCeiling", "NIFTY"));
        Assert.True(live.GetBool("EnableEmaGapFilter", "NIFTY", false));
        Assert.Equal(50m, changed.GetDecimal("CallRsiFloor", "NIFTY", 0));
        Assert.False(changed.IsSettingActive("CallRsiCeiling", "NIFTY"));
        Assert.False(changed.GetBool("EnableEmaGapFilter", "NIFTY", true));
    }

    [Fact]
    public void CatalogKeepsCallAndPutSeparateAndNamesUnique()
    {
        var grid = new GridOptions { MaxGridConfigurations = 5 };
        var call = ConfigurationCatalog.Build(TradeSide.Call, grid);
        var put = ConfigurationCatalog.Build(TradeSide.Put, grid);

        Assert.All(call, c => Assert.Equal(TradeSide.Call, c.Side));
        Assert.All(put, c => Assert.Equal(TradeSide.Put, c.Side));
        Assert.All(call.SelectMany(c => c.Changes), ch => Assert.DoesNotContain("Put", ch.Key));
        Assert.All(put.SelectMany(c => c.Changes), ch => Assert.DoesNotContain("Call", ch.Key));
        Assert.Equal(call.Count, call.Select(c => c.Name).Distinct().Count());
        Assert.True(call.Count(c => c.Name.Contains("|Grid|")) <= 5);
        Assert.Contains(call, c => c.Name == "CALL|Baseline" && c.Changes.Count == 0);
        Assert.Contains(call, c => c.Name == "CALL|NoRsi");
        Assert.Contains(call, c => c.Name == "CALL|NoEmaGap");
        Assert.Contains(call, c => c.Name == "CALL|NoPcr");
        Assert.Contains(call, c => c.Name == "CALL|NoAtr");
        Assert.Contains(call, c => c.Name.StartsWith("CALL|Rsi=") && c.Name.Contains("+Pcr="));
    }
}
