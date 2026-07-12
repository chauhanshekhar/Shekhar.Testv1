using Trading.Domain.Entities;

namespace Trading.Infrastructure.Brokers.Groww;

public class GrowwSettings
{
    public string AccessToken { get; set; } = "";
    public string BaseUrl { get; set; } = "https://api.groww.in";
    public TradingMode Mode { get; set; } = TradingMode.Paper;

    public bool IsPaperMode => Mode == TradingMode.Paper;
    public bool IsHeroZeroMode => Mode == TradingMode.HeroZero;
    public bool IsLiveMode => Mode == TradingMode.Live;
}
