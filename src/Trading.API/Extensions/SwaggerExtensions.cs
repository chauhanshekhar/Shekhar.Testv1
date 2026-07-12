using Microsoft.OpenApi;

namespace Trading.API.Extensions;

internal static class SwaggerExtensions
{
    internal static IServiceCollection AddSwagger(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddOpenApi();
        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new OpenApiInfo
            {
                Title       = "CRA",
                Version     = "v1",
                Description = "Trading Platform REST API"
            });
            c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name          = "Authorization",
                Type          = SecuritySchemeType.Http,
                Scheme        = "bearer",
                BearerFormat  = "JWT",
                In            = ParameterLocation.Header,
                Description   = "Enter your JWT token (without 'Bearer ' prefix). Example: eyJhbGci..."
            });
            c.AddSecurityRequirement(_ => new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("Bearer"), new List<string>() }
            });
        });
        return services;
    }

    internal static WebApplication UseSwaggerUi(this WebApplication app, IConfiguration cfg)
    {
        if (!app.Environment.IsDevelopment() && !cfg.GetValue<bool>("Swagger:Enabled"))
            return app;

        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json", "CRA v1");
            c.RoutePrefix    = "swagger";
            c.DocumentTitle  = "CRA";
            c.DefaultModelsExpandDepth(-1);
        });

        if (app.Environment.IsDevelopment())
            app.MapOpenApi();

        return app;
    }
}
