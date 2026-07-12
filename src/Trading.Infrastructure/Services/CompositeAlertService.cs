using Trading.Application.Interfaces;

namespace Trading.Infrastructure.Services;

/// <summary>
/// Fans out ITradingAlertService calls to Telegram and WhatsApp simultaneously.
/// </summary>
public sealed class CompositeAlertService : ITradingAlertService
{
    private readonly TelegramAlertService _telegram;
    private readonly WhatsAppAlertService _whatsApp;

    public CompositeAlertService(TelegramAlertService telegram, WhatsAppAlertService whatsApp)
    {
        _telegram = telegram;
        _whatsApp = whatsApp;
    }

    public Task SendCriticalAsync(string message)
        => Task.WhenAll(_telegram.SendCriticalAsync(message), _whatsApp.SendCriticalAsync(message));

    public Task SendTradeAlertAsync(string message)
        => Task.WhenAll(_telegram.SendTradeAlertAsync(message), _whatsApp.SendTradeAlertAsync(message));
}
