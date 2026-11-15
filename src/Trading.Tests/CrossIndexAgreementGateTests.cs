using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker.SignalGates;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// CrossIndexAgreementGate: take a signal only when the other indices moved in its direction over the lookback.
/// Replay of 263 closed signals: stop rate 35% when both others agreed vs 44% when they did not. See the gate's summary.
/// </summary>
public class CrossIndexAgreementGateTests
{
    private const string Call = TradingConst.SignalType.CALL, Put = TradingConst.SignalType.PUT;

    private static IReadOnlyList<(string Symbol, decimal? Move)> Moves(decimal? a, decimal? b) =>
        new List<(string, decimal?)> { ("NIFTY", a), ("SENSEX", b) };

    [Fact]
    public void Call_BothOthersUp_Proceeds()
        => Assert.Null(CrossIndexAgreementGate.Evaluate(Call, Moves(12m, 40m), 2));

    [Fact]
    public void Call_OneOtherDown_Blocks()
    {
        var reason = CrossIndexAgreementGate.Evaluate(Call, Moves(12m, -40m), 2);

        Assert.NotNull(reason);
        Assert.Contains("only 1 of 2", reason);
    }

    [Fact]
    public void Put_BothOthersDown_Proceeds()
        => Assert.Null(CrossIndexAgreementGate.Evaluate(Put, Moves(-5m, -30m), 2));

    [Fact]
    public void Put_OtherUp_Blocks()
        => Assert.NotNull(CrossIndexAgreementGate.Evaluate(Put, Moves(-5m, 30m), 2));

    [Fact]
    public void FlatMove_IsNotAgreement()
        => Assert.NotNull(CrossIndexAgreementGate.Evaluate(Call, Moves(0m, 40m), 2));

    [Fact]
    public void MinAgreeOne_NeedsOnlyOneOther()
        => Assert.Null(CrossIndexAgreementGate.Evaluate(Call, Moves(12m, -40m), 1));

    [Fact]
    public void MissingIndexIsNotCounted_RemainingOneMustAgree()
    {
        Assert.Null(CrossIndexAgreementGate.Evaluate(Call, Moves(12m, null), 2));
        Assert.NotNull(CrossIndexAgreementGate.Evaluate(Call, Moves(-12m, null), 2));
    }

    [Fact]
    public void NoDataForAnyOther_FailsOpen()
        => Assert.Null(CrossIndexAgreementGate.Evaluate(Call, Moves(null, null), 2));

    // ── SignedMove: closed bars only ─────────────────────────────────────────────────────────

    private static readonly DateTime Now = new(2026, 10, 26, 6, 30, 30, DateTimeKind.Utc);   // 12:00:30 IST

    private static List<Candle> Bars(int count, decimal firstClose, decimal step, int lastBarStartMinutesAgo = 1)
    {
        var list = new List<Candle>();
        for (var i = 0; i < count; i++)
            list.Add(new Candle
            {
                Symbol = "NIFTY_1m",
                CandleTime = Now.AddMinutes(-lastBarStartMinutesAgo - (count - 1 - i)).AddSeconds(-30),
                Close = firstClose + step * i,
            });
        return list;
    }

    [Fact]
    public void SignedMove_IsLastClosedCloseMinusFirst()
    {
        // 15 closed bars, +2 per bar: 14 steps = +28
        Assert.Equal(28m, CrossIndexAgreementGate.SignedMove(Bars(15, 24000m, 2m), 15, Now));
    }

    [Fact]
    public void SignedMove_IgnoresTheFormingBar()
    {
        var bars = Bars(15, 24000m, 2m);
        // a still-forming bar that started 10 seconds ago and spiked: must not be read
        bars.Add(new Candle { Symbol = "NIFTY_1m", CandleTime = Now.AddSeconds(-10), Close = 99999m });

        Assert.Equal(28m, CrossIndexAgreementGate.SignedMove(bars, 15, Now));
    }

    [Fact]
    public void SignedMove_StaleFeed_IsNull()
    {
        // newest closed bar started 20 minutes ago: that index has stopped updating, so it must not be read as "now"
        Assert.Null(CrossIndexAgreementGate.SignedMove(Bars(15, 24000m, 2m, lastBarStartMinutesAgo: 20), 15, Now));
    }

    [Fact]
    public void SignedMove_GapInsideTheWindow_IsNull()
    {
        var bars = Bars(15, 24000m, 2m);
        bars.RemoveRange(2, 8);   // 7 bars left spread over 15 minutes: enough bars, but not the last 15 minutes
        bars.Insert(0, new Candle { Symbol = "NIFTY_1m", CandleTime = Now.AddHours(-3), Close = 23000m });

        Assert.Null(CrossIndexAgreementGate.SignedMove(bars, 15, Now));
    }

    [Fact]
    public void SignedMove_TooFewClosedBars_IsNull()
        => Assert.Null(CrossIndexAgreementGate.SignedMove(Bars(5, 24000m, 2m), 15, Now));

    // ── side scope and wiring (all other dependencies null: the gate must not touch them) ────

    [Theory]
    [InlineData(0, "CALL", true)]
    [InlineData(0, "PUT", true)]
    [InlineData(1, "CALL", true)]
    [InlineData(1, "PUT", false)]
    [InlineData(2, "PUT", true)]
    [InlineData(2, "CALL", false)]
    public void Side_ScopesTheGate(int side, string signalType, bool applies)
        => Assert.Equal(applies, CrossIndexAgreementGate.AppliesTo(side, signalType));

    [Fact]
    public async Task DisabledByDefault_Proceeds()
        => Assert.Null(await Block(new FakeStrategySettingsService(), Call));

    [Fact]
    public async Task HeroZero_IsExempt()
        => Assert.Null(await Block(new FakeStrategySettingsService().Set(CrossIndexAgreementGate.EnableKey, 1), Call,
            TradingConst.StrategyType.HeroZero));

    [Fact]
    public async Task Enabled_SideNotCovered_Proceeds()
    {
        var settings = new FakeStrategySettingsService()
            .Set(CrossIndexAgreementGate.EnableKey, 1)
            .Set(CrossIndexAgreementGate.SideKey, CrossIndexAgreementGate.SideCallOnly);

        Assert.Null(await Block(settings, Put));   // PUT is out of scope: no candle or instrument lookup happens
    }

    private static Task<string?> Block(FakeStrategySettingsService settings, string signalType,
        string strategyType = TradingConst.StrategyType.Single) =>
        new CrossIndexAgreementGate().BlockAsync(Context(settings), Snapshot(),
            new Signal { Symbol = "BANKNIFTY", SignalType = signalType, StrategyType = strategyType }, CancellationToken.None);

    private static SignalContext Context(FakeStrategySettingsService settings) => new(
        Symbol: "BANKNIFTY",
        CandleRepo: null!, SignalRepo: null!, OrderRepo: null!, OptionRepo: null!,
        SettingsRepo: null!, PnlRepo: null!, JournalRepo: null!, EventRepo: null!, TickRepo: null!,
        Ema: null!, Rsi: null!, Atr: null!, Pcr: null!, Oi: null!, Regime: null!, Engine: null!,
        HeroZeroEngine: null!, Risk: null!, Orders: null!, LiveData: null!, Hub: null!, Uow: null!,
        Settings: new TradingSettings(), EventCalendar: null!, Instruments: null!,
        SystemSettings: null!, RuntimeSettings: null!, StrategySettings: settings);

    private static MarketSnapshot Snapshot() => new(
        Candles1m: new List<Candle>(),
        Candles5m: new List<Candle>(),
        OptionChain: new List<OptionChainData>(),
        PrevOiByKey: new Dictionary<(decimal, string), long>(),
        MarketRegime: new MarketRegime(MarketTrend.Uptrend, MarketVolatility.High, false, false, false, 0m),
        Ema20: 0m, Ema50: 0m, Ema20_5m: 0m, Ema50_5m: 0m,
        Rsi: 0m, Rsi5m: null, AtrPct: 0.050m, AtrRaw: 48m, Ltp: 55000m, YesterdayClose: 0m,
        CurrentVolume: 0m, AvgVolume20: 0m, PriceChangePct: 0m,
        DayOpen: 0m, DayLow: 0m, DayHigh: 0m, DayChangePct: 0m, Vwap: null, PcrValue: 0m,
        StrategyHint: "", NewsEvent: null,
        LastTickAge: TimeSpan.Zero, LastOptionChainAge: TimeSpan.Zero, LastClosed5mCandle: null);
}
