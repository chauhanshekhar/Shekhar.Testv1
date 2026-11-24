using Trading.Domain;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Services;

namespace Trading.SignalEngine.Services;

/// <summary>
/// ATM strike resolution and liquid CE/PE leg lookup, shared by every path that needs an
/// option leg (the CALL/PUT AND-chain's delta/OI inputs, and the leg actually traded in
/// <see cref="SignalResultFactory"/>).
/// </summary>
public class OptionLegSelector
{
    private readonly IInstrumentCache _instrumentCache;
    private readonly SystemSettingsService _settingsService;

    public OptionLegSelector(IInstrumentCache instrumentCache, SystemSettingsService settingsService)
    {
        _instrumentCache = instrumentCache;
        _settingsService = settingsService;
    }

    // Resolved once per call — cached 60s in SystemSettingsService
    private TradingSettings Settings => _settingsService.GetAsync().GetAwaiter().GetResult();

    public decimal ResolveAtmStrike(string symbol, decimal ltp, List<OptionChainData> optionChain)
    {
        var interval = (decimal)_instrumentCache.GetStrikeInterval(symbol);
        var spot = optionChain
            .Where(x => x.UnderlyingLtp > 0)
            .Select(x => x.UnderlyingLtp)
            .FirstOrDefault();

        if (spot <= 0)
            spot = ltp;

        return Math.Round(spot / interval, MidpointRounding.AwayFromZero) * interval;
    }

    public OptionChainData? SelectAtmLeg(string symbol, List<OptionChainData> chain, string optionType, decimal atmStrike) =>
        chain
            .Where(x => x.OptionType == optionType && x.Ltp > 0)
            .OrderBy(x => Math.Abs(x.StrikePrice - atmStrike))
            .ThenByDescending(x => x.Volume)
            .ThenByDescending(x => x.OpenInterest)
            .FirstOrDefault();
}
