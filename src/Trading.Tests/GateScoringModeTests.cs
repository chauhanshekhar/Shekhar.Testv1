using Trading.Domain.Entities;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// AtrGate and MarketRegimeGate are the two gates this session actually changed — both branch on
/// EnableSignalScoring (default false). These tests cover: (a) legacy behavior is byte-identical
/// to before scoring existed when the flag is off/absent, and (b) the new scored-mode behavior
/// (lower absolute ATR floor; Low volatility passes through, Extreme still hard-blocks).
/// The other gates listed in the spec's regression section (KillSwitch, EventBlackout, StaleData,
/// MaxDailyTrades, CircuitBreaker, ConsecutiveLoss, OnePosition, DuplicateSignal, RiskGate,
/// RiskRewardGate, SpreadGate) were not touched in this session — verified by diff, not by test,
/// since exercising them requires mocking broker/DB infrastructure disproportionate to a change
/// that never touched those files.
/// </summary>
public class GateScoringModeTests
{
    private const string Symbol = "NIFTY";

    private static SignalContext MakeContext(FakeStrategySettingsService settings) => new(
        Symbol: Symbol,
        CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
        SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
        Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
        HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
        Settings: null!, EventCalendar: null!, Instruments: null!, SystemSettings: null!,
        RuntimeSettings: null!, StrategySettings: settings);

    private static MarketSnapshot MakeSnapshot(decimal atrPct, MarketRegime? regime = null) => new(
        Candles1m: [], Candles5m: [], OptionChain: [], PrevOiByKey: [],
        MarketRegime: regime ?? new MarketRegime(MarketTrend.Sideways, MarketVolatility.High, false, false, false, 0m),
        Ema20: 0, Ema50: 0, Ema20_5m: 0, Ema50_5m: 0, Rsi: 0, AtrPct: atrPct, AtrRaw: 0,
        Ltp: 0, YesterdayClose: 0, CurrentVolume: 0, AvgVolume20: 0, PriceChangePct: 0, Vwap: 0,
        PcrValue: 0, StrategyHint: "", NewsEvent: null, LastTickAge: TimeSpan.Zero,
        LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null);

    // ── AtrGate ──────────────────────────────────────────────────────────────
    [Fact]
    public async Task AtrGate_LegacyMode_BlocksBelowMinAtrPercent_Unchanged()
    {
        var settings = new FakeStrategySettingsService().Set("MinAtrPercent", 0.04m); // scoring flag absent -> off
        var gate = new AtrGate();

        var blocked = await gate.BlockAsync(MakeContext(settings), MakeSnapshot(atrPct: 0.03m), CancellationToken.None);
        Assert.NotNull(blocked);

        var passed = await gate.BlockAsync(MakeContext(settings), MakeSnapshot(atrPct: 0.05m), CancellationToken.None);
        Assert.Null(passed);
    }

    [Fact]
    public async Task AtrGate_ScoredMode_UsesLowerAbsoluteFloor_NotQualityThreshold()
    {
        var settings = new FakeStrategySettingsService()
            .Set("EnableSignalScoring", 1)
            .Set("MinAtrPercent", 0.04m)
            .Set("MinAtrPercentAbsolute", 0.01m);
        var gate = new AtrGate();

        // Below the quality bar (0.04) but above the absolute safety floor (0.01) -> must pass,
        // otherwise the scorer would never see low-but-tradable ATR setups (the whole point).
        var passed = await gate.BlockAsync(MakeContext(settings), MakeSnapshot(atrPct: 0.02m), CancellationToken.None);
        Assert.Null(passed);

        // Below the absolute safety floor -> still hard-blocked, safety is never fully removed.
        var blocked = await gate.BlockAsync(MakeContext(settings), MakeSnapshot(atrPct: 0.005m), CancellationToken.None);
        Assert.NotNull(blocked);
    }

    // ── MarketRegimeGate ─────────────────────────────────────────────────────
    [Fact]
    public async Task MarketRegimeGate_LegacyMode_BlocksLowAndExtreme_Unchanged()
    {
        var settings = new FakeStrategySettingsService(); // scoring flag absent -> off
        var gate = new MarketRegimeGate();

        var lowVol = new MarketRegime(MarketTrend.Sideways, MarketVolatility.Low, false, false, false, 0m);
        var blockedLow = await gate.BlockAsync(MakeContext(settings), MakeSnapshot(0m, lowVol), CancellationToken.None);
        Assert.NotNull(blockedLow);

        var extremeVol = new MarketRegime(MarketTrend.Sideways, MarketVolatility.Extreme, false, false, false, 0m);
        var blockedExtreme = await gate.BlockAsync(MakeContext(settings), MakeSnapshot(0m, extremeVol), CancellationToken.None);
        Assert.NotNull(blockedExtreme);

        var highVol = new MarketRegime(MarketTrend.Uptrend, MarketVolatility.High, false, false, false, 0m);
        var passed = await gate.BlockAsync(MakeContext(settings), MakeSnapshot(0m, highVol), CancellationToken.None);
        Assert.Null(passed);
    }

    [Fact]
    public async Task MarketRegimeGate_ScoredMode_LowPassesThrough_ExtremeStillBlocks()
    {
        var settings = new FakeStrategySettingsService().Set("EnableSignalScoring", 1);
        var gate = new MarketRegimeGate();

        var lowVol = new MarketRegime(MarketTrend.Sideways, MarketVolatility.Low, false, false, false, 0m);
        var passedLow = await gate.BlockAsync(MakeContext(settings), MakeSnapshot(0m, lowVol), CancellationToken.None);
        Assert.Null(passedLow); // no longer suppressed outright -> the score has to compensate instead

        var extremeVol = new MarketRegime(MarketTrend.Sideways, MarketVolatility.Extreme, false, false, false, 0m);
        var blockedExtreme = await gate.BlockAsync(MakeContext(settings), MakeSnapshot(0m, extremeVol), CancellationToken.None);
        Assert.NotNull(blockedExtreme); // genuinely unsafe -> still a hard block in both modes
    }
}
