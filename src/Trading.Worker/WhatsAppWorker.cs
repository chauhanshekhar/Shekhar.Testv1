using System.Net.Http.Headers;
using System.Text;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;
using static Trading.Domain.TradingConst;

namespace Trading.Worker;

// Polls for new signals every 10 s and sends a formatted WhatsApp message via Twilio.
public class WhatsAppWorker : BackgroundService
{
    private readonly ILogger<WhatsAppWorker> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _http;
    private readonly string? _apiUrl;
    private readonly string? _from;
    private readonly string? _to;
    private long _lastSignalId = 0;

    public WhatsAppWorker(
        IConfiguration config,
        IHttpClientFactory factory,
        ILogger<WhatsAppWorker> logger,
        IServiceScopeFactory scopeFactory)
    {
        _logger       = logger;
        _scopeFactory = scopeFactory;
        _http         = factory.CreateClient();

        var accountSid = config["WhatsApp:AccountSid"];
        var authToken  = config["WhatsApp:AuthToken"];
        _from = config["WhatsApp:From"];
        _to   = config["WhatsApp:To"];

        if (!string.IsNullOrWhiteSpace(accountSid) && !string.IsNullOrWhiteSpace(authToken))
        {
            _apiUrl = $"https://api.twilio.com/2010-04-01/Accounts/{accountSid}/Messages.json";
            var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{accountSid}:{authToken}"));
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_apiUrl) || string.IsNullOrWhiteSpace(_from) || string.IsNullOrWhiteSpace(_to))
        {
            _logger.LogWarning("WhatsApp not configured — WhatsAppWorker idle.");
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
                _logger.LogError(ex, "WhatsAppWorker error");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }

    private async Task CheckNewSignals()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepository<Signal>>();

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
        var istTime = TimeZoneInfo.ConvertTimeFromUtc(signal.CreatedOn,
            TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));

        string tradeInfo = signal.OptionLtp > 0
            ? $"Option: {signal.OptionSymbol}\n" +
              $"Premium: ₹{signal.OptionLtp:F2}  Qty: {signal.Quantity}\n" +
              $"SL: ₹{signal.OptionSL:F2}  T1: ₹{signal.OptionT1:F2}  T2: ₹{signal.OptionT2:F2}"
            : $"Index SL: ₹{signal.StopLoss:F2}  T1: ₹{signal.Target1:F2}  T2: ₹{signal.Target2:F2}";

        var message =
            $"{emoji} {signal.SignalType} — {signal.Symbol}\n" +
            $"Index @ ₹{signal.EntryPrice:F2}  Confidence: {signal.Confidence:F0}%\n" +
            tradeInfo + "\n" +
            $"Strategy: {signal.StrategyType}\n" +
            $"{istTime:dd-MMM-yyyy HH:mm} IST";

        await SendAsync(message);
    }

    private async Task SendAsync(string text)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["From"] = _from!,
                ["To"]   = _to!,
                ["Body"] = text
            });

            var response = await _http.PostAsync(_apiUrl, content);
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("WhatsApp alert sent");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WhatsAppWorker: failed to send message");
        }
    }
}
