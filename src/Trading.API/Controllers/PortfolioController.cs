using Trading.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Trading.Domain.Entities;
using Trading.Infrastructure;
using Trading.Infrastructure.Repositories;
using Trading.SignalEngine.Services;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PortfolioController : ControllerBase
{
    private readonly IRepository<Portfolio> _portfolioRepo;
    private readonly IRepository<Position> _positionRepo;
    private readonly PortfolioService _portfolioService;
    private readonly IOptions<TradingSettings> _settings;
    private readonly Trading.Application.Interfaces.IUnitOfWork _uow;

    public PortfolioController(
        IRepository<Portfolio> portfolioRepo,
        IRepository<Position> positionRepo,
        PortfolioService portfolioService,
        IOptions<TradingSettings> settings,
        Trading.Application.Interfaces.IUnitOfWork uow)
    {
        _portfolioRepo    = portfolioRepo;
        _positionRepo     = positionRepo;
        _portfolioService = portfolioService;
        _settings         = settings;
        _uow              = uow;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var capital    = _settings.Value.Capital;
        var portfolios = await _portfolioRepo.GetAllAsync();
        var existing   = portfolios.FirstOrDefault();

        var portfolio = existing ?? new Portfolio
        {
            Capital         = capital,
            UsedMargin      = 0,
            AvailableMargin = capital,
            NetPnL          = 0,
            UpdatedOn       = IstClock.Now
        };

        // Keep Capital in sync with appsettings
        portfolio.Capital = capital;

        var positions = await _positionRepo.GetAllAsync();
        var updated   = _portfolioService.UpdatePortfolio(portfolio, positions);

        // Persist the updated snapshot
        if (existing is null)
            await _portfolioRepo.AddAsync(updated);

        await _uow.SaveChangesAsync();

        return Ok(new
        {
            updated.Capital,
            updated.UsedMargin,
            updated.AvailableMargin,
            updated.NetPnL,
            updated.UpdatedOn,
            Utilisation = _portfolioService.PortfolioUtilisation(updated),
            TotalEquity = _portfolioService.TotalEquity(updated, positions),
            Positions   = positions,
            TopGainers  = _portfolioService.TopGainers(positions),
            TopLosers   = _portfolioService.TopLosers(positions)
        });
    }

    [HttpGet("positions")]
    public async Task<IActionResult> Positions()
    {
        var positions = await _positionRepo.GetAllAsync();
        return Ok(positions);
    }
}
