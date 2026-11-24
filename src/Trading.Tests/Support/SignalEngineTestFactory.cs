using Microsoft.Extensions.Logging.Abstractions;
using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.Infrastructure.Services;
using Trading.SignalEngine.Services;
using Trading.Tests.Fakes;

namespace Trading.Tests.Support;

/// <summary>Builds a real SignalEngineService wired to fakes/no-ops for its infra dependencies,
/// so tests exercise the actual scoring/legacy decision logic rather than a mock of it.</summary>
public static class SignalEngineTestFactory
{
    public const string Symbol = "NIFTY";

    public static SignalEngineService Create(FakeStrategySettingsService settings)
    {
        var systemSettings = new SystemSettingsService(new StaticOptionsMonitor<TradingSettings>(new TradingSettings()));
        return new SignalEngineService(
            new FakeInstrumentCache(),
            systemSettings,
            settings,
            new AtrRiskService(),
            new OiAnalysisService(),
            null!, // IRepository<OptionChainData> — unused by GenerateSignalDetailed
            NullLogger<SignalEngineService>.Instance);
    }

    public static Candle MakeCandle(decimal open, decimal close, decimal high, decimal low) =>
        new()
        {
            Open = open,
            Close = close,
            High = high,
            Low = low,
            CandleTime = DateTime.UtcNow,
            Symbol = Symbol,
        };

    /// <summary>ATM CE/PE pair, priced for NIFTY's default TradingSettings
    /// (Volume>=200000, OI>=20000, Ltp in [20,300]) — strike computed from spot at the default
    /// 50-point interval (FakeInstrumentCache falls back to that via GetStrikeInterval).</summary>
    public static List<OptionChainData> BuildChain(decimal spot, long ceChangeInOi = 0, long peChangeInOi = 0)
    {
        var strike = Math.Round(spot / 50m, MidpointRounding.AwayFromZero) * 50m;
        return
        [
            new OptionChainData
            {
                Symbol = Symbol, TradingSymbol = "NIFTYTESTCE", StrikePrice = strike,
                OptionType = TradingConst.OptionType.CE, Ltp = 100m, Volume = 300_000,
                OpenInterest = 30_000, ChangeInOi = ceChangeInOi, Delta = 0.5m, UnderlyingLtp = 0m,
            },
            new OptionChainData
            {
                Symbol = Symbol, TradingSymbol = "NIFTYTESTPE", StrikePrice = strike,
                OptionType = TradingConst.OptionType.PE, Ltp = 100m, Volume = 300_000,
                OpenInterest = 30_000, ChangeInOi = peChangeInOi, Delta = -0.5m, UnderlyingLtp = 0m,
            },
        ];
    }
}
