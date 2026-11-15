using Trading.Application.Interfaces;
using Trading.Infrastructure;
using Trading.Worker;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// StrategySettings AdaptiveLotMin can size one symbol differently (BANKNIFTY 2 lots while NIFTY/SENSEX
/// keep the appsettings 4). No row -> the appsettings value, so nothing changes until a row is added.
/// </summary>
public class AdaptiveLotPerSymbolTests
{
    [Theory]
    [InlineData("BANKNIFTY", 2)]
    [InlineData("NIFTY", 4)]
    [InlineData("SENSEX", 4)]
    public async Task SymbolRow_OverridesOnlyThatSymbol(string symbol, int expectedLots)
    {
        var settings = new PerSymbolSettings()
            .Set(AdaptiveLotSizingService.EnableDailyLossLimitKey, null, 0)
            .Set(AdaptiveLotSizingService.AdaptiveLotMinKey, "BANKNIFTY", 2);

        var decision = await new AdaptiveLotSizingService().DetermineAsync(Context(symbol, settings), null!, 60m);

        Assert.Equal(expectedLots, decision.Lots);
    }

    [Fact]
    public async Task NoRow_UsesAppsettingsValue()
    {
        var settings = new PerSymbolSettings().Set(AdaptiveLotSizingService.EnableDailyLossLimitKey, null, 0);

        var decision = await new AdaptiveLotSizingService().DetermineAsync(Context("BANKNIFTY", settings), null!, 60m);

        Assert.Equal(4, decision.Lots);
    }

    [Fact]
    public async Task ZeroOrNegativeRow_NeverSizesBelowOneLot()
    {
        var settings = new PerSymbolSettings()
            .Set(AdaptiveLotSizingService.EnableDailyLossLimitKey, null, 0)
            .Set(AdaptiveLotSizingService.AdaptiveLotMinKey, "BANKNIFTY", 0);

        var decision = await new AdaptiveLotSizingService().DetermineAsync(Context("BANKNIFTY", settings), null!, 60m);

        Assert.Equal(1, decision.Lots);
    }

    private static SignalContext Context(string symbol, IStrategySettingsService settings) => new(
        Symbol: symbol,
        CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
        SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
        Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
        HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
        Settings: new TradingSettings { AdaptiveLotMin = 4 }, EventCalendar: null!, Instruments: null!,
        SystemSettings: null!, RuntimeSettings: null!, StrategySettings: settings);

    /// <summary>Symbol-aware fake: exact symbol row, then global (null) row, then the caller's default.</summary>
    private sealed class PerSymbolSettings : IStrategySettingsService
    {
        private readonly Dictionary<(string Key, string Symbol), decimal> _rows = new();

        public PerSymbolSettings Set(string key, string? symbol, decimal value)
        {
            _rows[(key.ToUpperInvariant(), symbol?.ToUpperInvariant() ?? "")] = value;
            return this;
        }

        private bool TryGet(string key, string? symbol, out decimal value)
            => _rows.TryGetValue((key.ToUpperInvariant(), symbol?.ToUpperInvariant() ?? ""), out value)
            || _rows.TryGetValue((key.ToUpperInvariant(), ""), out value);

        public decimal GetDecimal(string k, string? s, decimal d) => TryGet(k, s, out var v) ? v : d;
        public int GetInt(string k, string? s, int d) => TryGet(k, s, out var v) ? (int)v : d;
        public bool GetBool(string k, string? s, bool d) => TryGet(k, s, out var v) ? v != 0m : d;
        public string GetString(string k, string? s, string d) => TryGet(k, s, out var v) ? v.ToString(System.Globalization.CultureInfo.InvariantCulture) : d;
        public bool IsSettingActive(string k, string? s) => TryGet(k, s, out _);
        public Task ReloadAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
