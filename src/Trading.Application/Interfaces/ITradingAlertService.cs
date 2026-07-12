namespace Trading.Application.Interfaces;

public interface ITradingAlertService
{
    Task SendCriticalAsync(string message);
    Task SendTradeAlertAsync(string message);
}
