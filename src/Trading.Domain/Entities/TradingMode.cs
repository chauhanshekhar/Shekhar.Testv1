namespace Trading.Domain.Entities;

public enum TradingMode
{
    Paper = 1,   // No broker API — writes to PaperTrades table only
    Live = 2,   // Real orders with full configured lot size
    HeroZero = 3,   // Real orders but forced to 1 lot — validates signals with minimal risk
}
