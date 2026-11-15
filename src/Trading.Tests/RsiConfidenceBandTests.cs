using Trading.Domain;
using Trading.Tests.Fakes;
using Trading.Tests.Support;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// The confidence score's RSI contribution reads the SAME Call/PutRsiFloor/Ceiling band the entry
/// chains gate on. The legacy look-alike keys (RsiCallFloor/Ceiling, RsiPutFloor/Ceiling) differ
/// only in word order and used to feed confidence - and therefore lot sizing - on their own. They
/// must now have no effect at all.
///
/// Every other confidence input is pinned to zero so the score is 25 (base) + RSI:
/// EMA20 == EMA50 (no separation bonus), PCR 1.00 (no bonus either side), no candle (body% 0).
/// </summary>
public class RsiConfidenceBandTests
{
    private const decimal Spot = 20_000m;
    private const decimal Base = 25m;

    private static decimal Confidence(FakeStrategySettingsService settings, bool call, decimal rsi)
    {
        var engine = SignalEngineTestFactory.Create(settings);

        engine.GenerateRuleSignal(
            symbol: SignalEngineTestFactory.Symbol,
            optionType: call ? TradingConst.OptionType.CE : TradingConst.OptionType.PE,
            call: call,
            ltp: Spot,
            ema20: Spot,
            ema50: Spot,
            pcr: 1.00m,
            rsi: rsi,
            currentVolume: 0m,
            avgVolume20: 0m,
            atrRaw: 30m,
            optionChain: SignalEngineTestFactory.BuildChain(Spot),
            lastClosedCandle: null,
            out var confidence);

        return confidence;
    }

    // ── CALL ────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(50, 0)]     // below CallRsiFloor
    [InlineData(60, 8)]     // inside the band
    [InlineData(65, 15)]    // at CallRsiCeiling
    public void Call_ScoresAgainstEntryGateBand_NotLegacyKeys(int rsi, int rsiBonus)
    {
        var settings = new FakeStrategySettingsService()
            .Set("CallRsiFloor", 53m)
            .Set("CallRsiCeiling", 65m)
            // Legacy keys set to values that would give a different score if they were still read.
            .Set("RsiCallFloor", 20m)
            .Set("RsiCallCeiling", 30m);

        Assert.Equal(Base + rsiBonus, Confidence(settings, call: true, rsi));
    }

    [Fact]
    public void Call_LegacyKeysAlone_ContributeNothing()
    {
        var settings = new FakeStrategySettingsService()
            .Set("RsiCallFloor", 53m)
            .Set("RsiCallCeiling", 65m);

        Assert.Equal(Base, Confidence(settings, call: true, rsi: 60m));
    }

    [Fact]
    public void Call_InactiveEntryBand_SkipsRsiContribution()
    {
        var settings = new FakeStrategySettingsService()
            .Set("CallRsiFloor", 53m)
            .Set("CallRsiCeiling", 65m)
            .SetInactive("CallRsiCeiling");

        Assert.Equal(Base, Confidence(settings, call: true, rsi: 60m));
    }

    // ── PUT ─────────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(25, 15)]    // at/below PutRsiFloor
    [InlineData(35, 8)]     // inside the band
    [InlineData(45, 0)]     // above PutRsiCeiling (the legacy 55 would have scored +8)
    public void Put_ScoresAgainstEntryGateBand_NotLegacyKeys(int rsi, int rsiBonus)
    {
        var settings = new FakeStrategySettingsService()
            .Set("PutRsiFloor", 28m)
            .Set("PutRsiCeiling", 40m)
            .Set("RsiPutFloor", 30m)
            .Set("RsiPutCeiling", 55m);

        Assert.Equal(Base + rsiBonus, Confidence(settings, call: false, rsi));
    }

    [Fact]
    public void Put_LegacyKeysAlone_ContributeNothing()
    {
        var settings = new FakeStrategySettingsService()
            .Set("RsiPutFloor", 28m)
            .Set("RsiPutCeiling", 45m);

        Assert.Equal(Base, Confidence(settings, call: false, rsi: 35m));
    }

    [Fact]
    public void Put_ConfidenceIsNotTiedToEnableRsiFilter()
    {
        // EnableRsiFilter switches the PUT entry condition only; the confidence contribution
        // follows the band's own IsActive flags.
        var settings = new FakeStrategySettingsService()
            .Set("EnableRsiFilter", 0)
            .Set("PutRsiFloor", 28m)
            .Set("PutRsiCeiling", 40m);

        Assert.Equal(Base + 8m, Confidence(settings, call: false, rsi: 35m));
    }
}
