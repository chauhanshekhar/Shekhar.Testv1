using Trading.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly IRepository<SubscriptionPlan> _plans;
    private readonly IRepository<UserSubscription> _subs;
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;

    public SubscriptionController(
        IRepository<SubscriptionPlan> plans,
        IRepository<UserSubscription> subs,
        IRepository<User> users,
        IUnitOfWork uow)
    {
        _plans = plans;
        _subs = subs;
        _users = users;
        _uow = uow;
    }

    // ── Plans ──────────────────────────────────────────────────────────────

    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans()
    {
        var plans = await _plans.GetAllAsync();
        return Ok(plans.OrderBy(p => p.Price).Select(p => new
        {
            p.Id,
            p.Name,
            p.Price,
            p.DurationDays,
            p.IsActive,
            p.CreatedOn,
            features = ParseFeatures(p.Features)
        }));
    }

    [HttpPost("plans")]
    [Authorize(Roles = "Admin,Super Admin")]
    public async Task<IActionResult> CreatePlan([FromBody] PlanRequest req)
    {
        var plan = new SubscriptionPlan
        {
            Name = req.Name,
            Price = req.Price,
            DurationDays = req.DurationDays,
            Features = JsonSerializer.Serialize(req.Features ?? []),
            IsActive = req.IsActive,
            CreatedOn = IstClock.Now
        };
        await _plans.AddAsync(plan);
        await _uow.SaveChangesAsync();
        return Ok(plan);
    }

    [HttpPatch("plans/{id:int}/toggle")]
    [Authorize(Roles = "Admin,Super Admin")]
    public async Task<IActionResult> TogglePlan(int id)
    {
        var plan = await _plans.FirstOrDefaultAsync(p => p.Id == id);
        if (plan is null) return NotFound();
        plan.IsActive = !plan.IsActive;
        await _uow.SaveChangesAsync();
        return Ok(new { plan.Id, plan.IsActive });
    }

    // ── User subscriptions ─────────────────────────────────────────────────

    [HttpGet("users")]
    [Authorize(Roles = "Admin,Super Admin")]
    public async Task<IActionResult> GetUserSubs()
    {
        var subs = await _subs.GetAllAsync();
        var users = (await _users.GetAllAsync()).ToDictionary(u => u.Id);

        return Ok(subs.OrderByDescending(s => s.StartDate).Select(s =>
        {
            users.TryGetValue(s.UserId, out var u);
            return new
            {
                s.Id,
                s.UserId,
                s.PlanId,
                s.PlanName,
                s.StartDate,
                s.ExpiryDate,
                s.IsActive,
                s.AutoRenew,
                userName = u?.FullName ?? "",
                email = u?.Email ?? ""
            };
        }));
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private static List<string> ParseFeatures(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}

public record PlanRequest(
    string Name,
    decimal Price,
    int DurationDays,
    List<string>? Features,
    bool IsActive);
