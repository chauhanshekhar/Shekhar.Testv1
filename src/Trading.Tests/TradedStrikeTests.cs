using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;
using Trading.Worker;
using Xunit;

namespace Trading.Tests;

/// <summary>
/// Signal.StrikePrice must be the strike of the option actually traded (OptionSymbol), not the
/// ATM strike. OptionLegSelector picks the nearest LIQUID strike, which on 15 of 86 signals since
/// 2026-09-01 was a neighbour (round strikes carry the volume) - e.g. signal 124 stored 54400
/// while trading BANKNIFTY26OCT54500PE. Entry delta/OI must come from that same traded leg.
/// </summary>
public class TradedStrikeTests
{
    private const string Symbol = "NIFTY";

    private static OptionChainData Leg(string tradingSymbol, decimal strike, string type, long volume, decimal ltp, decimal delta) => new()
    {
        Symbol = Symbol, TradingSymbol = tradingSymbol, StrikePrice = strike, OptionType = type,
        Ltp = ltp, Volume = volume, OpenInterest = 30_000, Delta = delta, UnderlyingLtp = 0m,
    };

    // ── SignalResultFactory (rule path) ─────────────────────────────────────────────────────

    [Fact]
    public void Factory_IlliquidAtm_StoresTheTradedLegsStrike()
    {
        var systemSettings = new SystemSettingsService(new StaticOptionsMonitor<TradingSettings>(new TradingSettings()));
        var factory = new SignalResultFactory(
            new FakeInstrumentCache(), new AtrRiskService(), systemSettings,
            new OptionLegSelector(new FakeInstrumentCache(), systemSettings));

        var chain = new List<OptionChainData>
        {
            Leg("NIFTYTEST20000CE", 20_000m, TradingConst.OptionType.CE, 1_000, 100m, 0.50m),     // ATM, below the volume floor
            Leg("NIFTYTEST20050CE", 20_050m, TradingConst.OptionType.CE, 300_000, 80m, 0.45m),    // liquid neighbour
        };

        var signal = factory.Build(Symbol, 20_000m, 30m, chain, 20_000m, TradingConst.OptionType.CE, 60m, call: true).Signal;

        Assert.NotNull(signal);
        Assert.Equal("NIFTYTEST20050CE", signal!.OptionSymbol);
        Assert.Equal(20_050m, signal.StrikePrice);
        Assert.Equal(80m, signal.OptionLtp);
    }

    // ── Entry-leg lookup (placement DeltaAtEntry + blocked-signal audit) ────────────────────

    [Fact]
    public void FindEntryLeg_SymbolWinsOverAnEarlierStrikeMatch()
    {
        // The ATM row comes FIRST in the chain and matches the (old, wrong) stored strike; the old
        // OR-predicate returned it, so DeltaAtEntry came from the wrong option.
        var chain = new List<OptionChainData>
        {
            Leg("BANKNIFTY26OCT54400PE", 54_400m, TradingConst.OptionType.PE, 50_000, 754.60m, -0.48m),
            Leg("BANKNIFTY26OCT54500PE", 54_500m, TradingConst.OptionType.PE, 50_000, 794.55m, -0.55m),
        };
        var signal = new Signal { OptionSymbol = "BANKNIFTY26OCT54500PE", StrikePrice = 54_400m, OptionType = TradingConst.OptionType.PE };

        var leg = SignalAuditService.FindEntryLeg(chain, signal);

        Assert.NotNull(leg);
        Assert.Equal("BANKNIFTY26OCT54500PE", leg!.TradingSymbol);
        Assert.Equal(-0.55m, leg.Delta);
    }

    [Fact]
    public void FindEntryLeg_NoSymbol_FallsBackToStrikeAndType()
    {
        var chain = new List<OptionChainData>
        {
            Leg("A", 20_000m, TradingConst.OptionType.CE, 300_000, 100m, 0.50m),
            Leg("B", 20_000m, TradingConst.OptionType.PE, 300_000, 100m, -0.50m),
        };

        var leg = SignalAuditService.FindEntryLeg(chain, new Signal { OptionSymbol = "", StrikePrice = 20_000m, OptionType = TradingConst.OptionType.PE });

        Assert.Equal("B", leg?.TradingSymbol);
    }

    [Fact]
    public void FindEntryLeg_NothingMatches_ReturnsNull()
        => Assert.Null(SignalAuditService.FindEntryLeg(
            new List<OptionChainData> { Leg("A", 20_000m, TradingConst.OptionType.CE, 300_000, 100m, 0.50m) },
            new Signal { OptionSymbol = "ZZZ", StrikePrice = 19_000m, OptionType = TradingConst.OptionType.PE }));
}
