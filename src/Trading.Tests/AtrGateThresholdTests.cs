using Trading.Domain.Entities;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// AtrGate is a PRE-gate: it runs before StrategyManager, so it can suppress the BANKNIFTY PUT
/// BREAKDOWN rule before the rule is ever evaluated. The rule's own floor is AtrPct >= 0.030.
///
/// IsSettingActive("MinAtrPercent", symbol) reading "no row at all" as NOT active (not "falls
/// back to the compiled 0.10 default and keeps enforcing it") means the gate simply does not
/// apply until BANKNIFTY has a MinAtrPercent row — see docs/set-banknifty-min-atr-percent.sql to
/// configure the rule's own 0.030 floor once you want the gate active for this symbol again.
///
/// These tests pin that relationship in code so the dependency on a DB row is visible here rather
/// than only in an ops runbook.
/// </summary>
public class AtrGateThresholdTests
{
    private const string Symbol = "BANKNIFTY";

    private static SignalContext Context(FakeStrategySettingsService settings) => new(
        Symbol: Symbol,
        CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
        SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
        Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
        HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
        Settings: null!, EventCalendar: null!, Instruments: null!, SystemSettings: null!,
        RuntimeSettings: null!, StrategySettings: settings);

    /// <summary>AtrGate reads only MarketSnapshot.AtrPct, so the rest stays at defaults.</summary>
    private static MarketSnapshot Snapshot(decimal atrPct) => new(
        Candles1m: new List<Candle>(),
        Candles5m: new List<Candle>(),
        OptionChain: new List<OptionChainData>(),
        PrevOiByKey: new Dictionary<(decimal, string), long>(),
        MarketRegime: new MarketRegime(MarketTrend.Downtrend, MarketVolatility.High, false, false, false, 0m),
        Ema20: 0m, Ema50: 0m, Ema20_5m: 0m, Ema50_5m: 0m,
        Rsi: 0m, Rsi5m: null, AtrPct: atrPct, AtrRaw: 0m, Ltp: 0m, YesterdayClose: 0m,
        CurrentVolume: 0m, AvgVolume20: 0m, PriceChangePct: 0m,
        DayOpen: 0m, DayLow: 0m, DayHigh: 0m, DayChangePct: 0m, Vwap: null, PcrValue: 0m,
        StrategyHint: "", NewsEvent: null,
        LastTickAge: TimeSpan.Zero, LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null);

    private static async Task<string?> Block(decimal atrPct, FakeStrategySettingsService settings) =>
        await new AtrGate().BlockAsync(Context(settings), Snapshot(atrPct), CancellationToken.None);

    // ── Without the DB row: the gate does not apply at all ──────────────────────────────────

    [Theory]
    [InlineData("0.000")]
    [InlineData("0.030")]
    [InlineData("0.050")]
    [InlineData("0.099")]
    public async Task WithoutBankNiftyRow_GateDoesNotApply(string atrPct)
    {
        // No MinAtrPercent row -> IsSettingActive is false -> the gate is skipped entirely, even
        // at ATR% = 0. An unconfigured symbol must not silently enforce the compiled 0.10
        // fallback nobody actually set for it.
        var settings = new FakeStrategySettingsService();

        var block = await Block(decimal.Parse(atrPct, System.Globalization.CultureInfo.InvariantCulture), settings);

        Assert.Null(block);
    }

    // ── With MinAtrPercent = 0.030: the rule's band reaches the engine ──────────────────────

    [Theory]
    [InlineData("0.030")]   // exactly the rule floor
    [InlineData("0.050")]
    [InlineData("0.099")]
    [InlineData("0.150")]
    public async Task WithBankNiftyRowAt0030_RuleBandPasses(string atrPct)
    {
        var settings = new FakeStrategySettingsService().Set("MinAtrPercent", 0.030m);

        var block = await Block(decimal.Parse(atrPct, System.Globalization.CultureInfo.InvariantCulture), settings);

        Assert.Null(block);
    }

    [Theory]
    [InlineData("0.0299")]
    [InlineData("0.0100")]
    [InlineData("0.0000")]
    public async Task WithBankNiftyRowAt0030_BelowFloorStillBlocks(string atrPct)
    {
        // Volatility protection is lowered, never removed — below 0.030 the gate must still fire.
        var settings = new FakeStrategySettingsService().Set("MinAtrPercent", 0.030m);

        var block = await Block(decimal.Parse(atrPct, System.Globalization.CultureInfo.InvariantCulture), settings);

        Assert.NotNull(block);
    }
}
