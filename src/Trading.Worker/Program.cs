using Trading.Application.Interfaces;
using Trading.Infrastructure;
using Trading.Worker.Extensions;
using Trading.Worker.Strategies;

EnvLoader.Load();

var builder = Host.CreateApplicationBuilder(args);
var cfg = builder.Configuration;

builder.Services
    .AddWorkerLogging(cfg)
    .AddDatabase(cfg)
    .AddBroker(cfg)
    .AddSignalEngine()
    .AddWorkers();

var host = builder.Build();

await host.Services.GetRequiredService<IInstrumentCache>().ReloadAsync();
await host.Services.GetRequiredService<IStrategySettingsService>().ReloadAsync();

// Fail fast if two instrument strategies claim the same symbol — resolution would otherwise be
// registration-order dependent, i.e. a live instrument trading under a strategy nobody chose.
// Runs after the instrument cache is loaded so it checks the symbols actually being traded.
using (var startupScope = host.Services.CreateScope())
{
    var activeSymbols = host.Services.GetRequiredService<IInstrumentCache>()
        .GetActive().Select(i => i.Symbol);

    startupScope.ServiceProvider
        .GetRequiredService<InstrumentStrategyRegistry>()
        .ValidateRegistrations(activeSymbols);
}

host.Run();
