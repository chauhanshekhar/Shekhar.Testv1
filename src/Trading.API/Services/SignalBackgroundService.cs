using Trading.Domain;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Services;

namespace Trading.API.Services;

// Runs signal engine every 10s inside the API process — no separate Worker project needed.
public class SignalBackgroundService : BackgroundService
{
    private readonly ILogger<SignalBackgroundService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IInstrumentCache _instrumentCache;

    public SignalBackgroundService(ILogger<SignalBackgroundService> logger, IServiceScopeFactory scopeFactory, IInstrumentCache instrumentCache)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
        _instrumentCache = instrumentCache;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (MarketHours.IsOpen())
            {
                foreach (var symbol in _instrumentCache.GetActive().Select(i => i.Symbol))
                {
                    try { await RunForSymbol(symbol); }
                    catch (Exception ex) { _logger.LogError(ex, "SignalBackgroundService error for {Symbol}", symbol); }
                }
            }
            else
            {
                _logger.LogDebug("Market closed — signals paused");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }


    private async Task RunForSymbol(string symbol)
    {
        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;

        var candleRepo = sp.GetRequiredService<IRepository<Candle>>();
        var signalRepo = sp.GetRequiredService<IRepository<Signal>>();
        var optionRepo = sp.GetRequiredService<IRepository<OptionChainData>>();
        var ema = sp.GetRequiredService<EmaService>();
        var rsiSvc = sp.GetRequiredService<RsiService>();
        var pcr = sp.GetRequiredService<PcrService>();
        var engine = sp.GetRequiredService<SignalEngineService>();
        var orders = sp.GetRequiredService<IOrderService>();
        var uow = sp.GetRequiredService<IUnitOfWork>();

        var dayStart = IstClock.Now.Date;
        var candles = (await candleRepo.FindAsync(c => c.Symbol == symbol && c.CandleTime >= dayStart))
            .OrderBy(c => c.CandleTime)
            .ToList();

        if (candles.Count < 51) return;

        var closes = candles.Select(c => c.Close).ToList();
        var ema20 = ema.GetEma(closes, 20);
        var ema50 = ema.GetEma(closes, 50);
        var ltp = candles[^1].Close;

        var chainCutoff = IstClock.Now.AddMinutes(-5);
        var chain = await optionRepo.FindAsync(o => o.Symbol == symbol && o.CreatedOn >= chainCutoff);
        var pcrVal = chain.Count > 0 ? pcr.Calculate(chain) : 1m;
        var rsiVal = rsiSvc.GetRsi(closes);

        var signal = engine.GenerateSignal(symbol, ltp, ema20, ema50, pcrVal, rsiVal,
            0m, 1m, 0m, 0m, 0m, chain);
        if (signal is null) return;

        await signalRepo.AddAsync(signal);

        var orderId = await orders.PlaceOrderAsync(signal);
        _logger.LogInformation("Signal {Type} {Symbol} @ {Price} — order {OrderId}",
            signal.SignalType, symbol, ltp, orderId);

        await uow.SaveChangesAsync();
    }
}
