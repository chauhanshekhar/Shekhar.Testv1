using Telegram.Bot;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using static Trading.Domain.TradingConst;

namespace Trading.Worker;

// Polls for new signals and sends Telegram alerts for every new signal found.
public class TelegramWorker : BackgroundService
{
    private readonly ILogger<TelegramWorker> _logger;
    private readonly TelegramBotClient? _bot;
    private readonly string _chatId;
    private readonly IServiceScopeFactory _scopeFactory;
    private long _lastSignalId = 0;

    public TelegramWorker(
        IConfiguration config,
        ILogger<TelegramWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;

        var token = config["Telegram:BotToken"] ?? "";
        _chatId = config["Telegram:ChatId"] ?? "";

        if (!string.IsNullOrWhiteSpace(token))
            _bot = new TelegramBotClient(token);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_bot is null || string.IsNullOrWhiteSpace(_chatId))
        {
            _logger.LogWarning("Telegram not configured — TelegramWorker idle.");
            return;
        }

        // Seed _lastSignalId from DB so a restart never re-sends already-sent signals.
        using (var seed = _scopeFactory.CreateScope())
        {
            var repo = seed.ServiceProvider.GetRequiredService<IRepository<Signal>>();
            var latest = await repo.FindAsync(
                s => true,
                q => q.OrderByDescending(s => s.Id).Take(1));
            _lastSignalId = latest.FirstOrDefault()?.Id ?? 0;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                  await CheckNewSignals();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TelegramWorker error");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task CheckNewSignals()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepository<Signal>>();

        // Filter in SQL — avoids loading the entire signals table on every 10-second tick
        var lastId = _lastSignalId;
        var newSignals = (await repo.FindAsync(s => s.Id > lastId))
            .OrderBy(s => s.Id)
            .ToList();

        foreach (var signal in newSignals)
        {
            await SendSignalAlert(signal);
            _lastSignalId = signal.Id;
        }
    }

    private async Task SendSignalAlert(Signal signal)
    {
        var emoji = signal.SignalType == SignalType.CALL ? "📈" : "📉";

        // Show option trade details when available, otherwise fall back to index levels.
        string tradeInfo = signal.OptionLtp > 0
            ? $"Option: *{signal.OptionSymbol}*\n" +
              $"Premium: ₹{signal.OptionLtp:F2}  Qty: {signal.Quantity}\n" +
              $"Option SL: ₹{signal.OptionSL:F2}  T1: ₹{signal.OptionT1:F2}  T2: ₹{signal.OptionT2:F2}"
            : $"Index SL: ₹{signal.StopLoss:F2}  T1: ₹{signal.Target1:F2}  T2: ₹{signal.Target2:F2}";

        var context =
            $"RSI: {signal.RsiAtEntry:F0}  PCR: {signal.PcrAtEntry:F2}  " +
            $"ATR: {signal.AtrPctAtEntry:F2}%  Delta: {signal.DeltaAtEntry:F2}";

        var message =
            $"{emoji} *{signal.SignalType} — {signal.Symbol}*\n" +
            $"Index @ ₹{signal.EntryPrice:F2}  Confidence: {signal.Confidence:F0}%\n" +
            context + "\n" +
            tradeInfo + "\n" +
            $"_{signal.CreatedOn:dd-MMM-yyyy HH:mm IST}_";

        await SendAsync(message);
    }

    private async Task SendAsync(string message)
    {
        if (_bot is null) return;

        try
        {
            await _bot.SendMessage(
                chatId: _chatId,
                text: message,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Telegram message");
        }
    }
}
