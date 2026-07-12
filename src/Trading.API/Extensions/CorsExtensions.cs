namespace Trading.API.Extensions;

internal static class CorsExtensions
{
    internal static IServiceCollection AddTradingCors(
        this IServiceCollection services, IConfiguration cfg)
    {
        var origins = (cfg["CorsOrigins"] ?? "http://localhost:4200")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        services.AddCors(o =>
            o.AddPolicy("TradingWeb", p =>
                p.WithOrigins(origins)
                 .AllowAnyHeader()
                 .AllowAnyMethod()
                 .AllowCredentials()));

        return services;
    }
}
