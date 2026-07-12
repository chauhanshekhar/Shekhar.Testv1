using Trading.Application.Interfaces;
using Trading.Infrastructure;
using Trading.Worker.Extensions;

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

host.Run();
