using Microsoft.EntityFrameworkCore;
using Trading.API.Extensions;
using Trading.API.Hubs;
using Trading.Application.Interfaces;
using Trading.Infrastructure;
using Trading.Infrastructure.Data;

EnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

builder.AddApiLogging();

builder.Services
    .AddDatabase(cfg)
    .AddBroker(cfg)
    .AddSignalEngine()
    .AddJwtAuth(cfg)
    .AddSwagger()
    .AddTradingCors(cfg)
    .AddControllers();

builder.Services.Configure<Microsoft.AspNetCore.Routing.RouteOptions>(o => o.LowercaseUrls = true);
builder.Services.AddSignalR();

var app = builder.Build();

if (app.Environment.IsDevelopment() || cfg.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<TradingDbContext>().Database.Migrate();
}

await app.Services.GetRequiredService<IInstrumentCache>().ReloadAsync();

app.UseSwaggerUi(cfg);
app.MapGet("/", () => Results.Redirect("/swagger/index.html"));
app.UseHttpsRedirection();
app.UseCors("TradingWeb");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<MarketHub>("/marketHub");
app.MapHub<SignalHub>("/signalHub");
app.MapHealthChecks("/health");

app.Run();
