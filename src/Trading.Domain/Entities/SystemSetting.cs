using Trading.Domain;
namespace Trading.Domain.Entities;

/// <summary>
/// Generic key-value store for worker state that must survive restarts.
/// Example keys:
///   "CandleBuilder:LastBuildTime:NIFTY"
///   "CandleBuilder:LastBuildTime:BANKNIFTY"
///   "HistoricalData:LastFetchDate:NIFTY"
/// </summary>
public class SystemSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTime UpdatedOn { get; set; } = IstClock.Now;
}
