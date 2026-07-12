namespace Trading.Application.Interfaces;

public record MarginInfo(
    decimal AvailableCash,
    decimal UsedMargin,
    decimal TotalCapital);

public interface IMarginService
{
    /// <summary>
    /// Returns the current available cash margin from the broker.
    /// Returns null if the broker call fails (caller decides fallback behaviour).
    /// </summary>
    Task<MarginInfo?> GetMarginAsync();
}
