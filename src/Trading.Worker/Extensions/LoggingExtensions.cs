using Serilog;

namespace Trading.Worker.Extensions;

internal static class LoggingExtensions
{
    internal static IServiceCollection AddWorkerLogging(
        this IServiceCollection services, IConfiguration cfg)
    {
        services.AddSerilog((sp, lc) => lc
            .ReadFrom.Configuration(cfg)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                "logs/worker-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));

        return services;
    }
}
