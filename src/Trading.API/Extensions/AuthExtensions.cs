using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace Trading.API.Extensions;

internal static class AuthExtensions
{
    internal static IServiceCollection AddJwtAuth(
        this IServiceCollection services, IConfiguration cfg)
    {
        var jwtKey = cfg["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(jwtKey))
            throw new InvalidOperationException(
                "Jwt:Key must be configured. Set it in appsettings or as an environment variable.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer           = true,
                    ValidateAudience         = true,
                    ValidateLifetime         = true,
                    ValidateIssuerSigningKey  = true,
                    ValidIssuer              = cfg["Jwt:Issuer"],
                    ValidAudience            = cfg["Jwt:Audience"],
                    IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };
                o.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        var path  = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(token) &&
                            (path.StartsWithSegments("/marketHub") || path.StartsWithSegments("/signalHub")))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();
        return services;
    }
}
