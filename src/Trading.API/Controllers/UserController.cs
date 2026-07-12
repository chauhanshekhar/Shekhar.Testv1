using Trading.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Trading.API.Services;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;

    public UserController(IRepository<User> users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    // ── Own profile ────────────────────────────────────────────────────────

    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var user = await CurrentUserAsync();
        if (user is null) return NotFound();
        return Ok(MapUser(user));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
    {
        var user = await CurrentUserAsync();
        if (user is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(req.FullName))
            user.FullName = req.FullName.Trim();
        await _users.UpdateAsync(user);
        return Ok(MapUser(user));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
    {
        var user = await CurrentUserAsync();
        if (user is null) return NotFound();

        var verification = PasswordSecurity.Verify(user, req.CurrentPassword);
        if (verification == PasswordVerificationResult.Failed)
            return BadRequest(new { message = "Current password is incorrect" });

        if (req.NewPassword.Length < 8)
            return BadRequest(new { message = "New password must be at least 8 characters" });

        user.PasswordHash = PasswordSecurity.Hash(user, req.NewPassword);
        await _users.UpdateAsync(user);
        return Ok(new { message = "Password changed" });
    }

    // ── Admin: manage all users ────────────────────────────────────────────

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll([FromQuery] string? search, [FromQuery] string? role)
    {
        var all = await _users.FindAsync(_ => true);

        if (!string.IsNullOrWhiteSpace(search))
            all = all.Where(u =>
                u.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                u.Email.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!string.IsNullOrWhiteSpace(role))
            all = all.Where(u => u.Role == role).ToList();

        return Ok(all.OrderByDescending(u => u.CreatedOn).Select(MapUser));
    }

    [HttpGet("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(int id)
    {
        var user = await _users.GetByIdAsync(id);
        if (user is null) return NotFound();
        return Ok(MapUser(user));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest req)
    {
        var existing = await _users.FirstOrDefaultAsync(u => u.Email.ToLower() == req.Email.ToLower());
        if (existing != null)
            return BadRequest(new { message = "Email already registered" });

        var user = new User
        {
            FullName = req.FullName.Trim(),
            Email = req.Email.Trim().ToLower(),
            Role = req.Role ?? "Trader",
            IsActive = true,
            CreatedOn = IstClock.Now
        };
        user.PasswordHash = PasswordSecurity.Hash(user, req.Password);

        await _users.AddAsync(user);
        return Ok(MapUser(user));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(int id, [FromBody] AdminUpdateUserRequest req)
    {
        var user = await _users.GetByIdAsync(id);
        if (user is null) return NotFound();
        user.FullName = req.FullName.Trim();
        user.Role = req.Role ?? user.Role;
        user.IsActive = req.IsActive;
        await _users.UpdateAsync(user);
        return Ok(MapUser(user));
    }

    [HttpPost("{id:int}/reset-password")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetPassword(int id, [FromBody] ResetPasswordRequest req)
    {
        var user = await _users.GetByIdAsync(id);
        if (user is null) return NotFound();
        if (req.NewPassword.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters" });
        user.PasswordHash = PasswordSecurity.Hash(user, req.NewPassword);
        await _users.UpdateAsync(user);
        return Ok(new { message = "Password reset" });
    }

    [HttpPost("{id:int}/toggle-active")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var user = await _users.GetByIdAsync(id);
        if (user is null) return NotFound();
        if (user.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Cannot deactivate your own account" });
        user.IsActive = !user.IsActive;
        await _users.UpdateAsync(user);
        return Ok(new { isActive = user.IsActive });
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        var user = await _users.GetByIdAsync(id);
        if (user is null) return NotFound();
        if (user.Email.Equals(email, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "Cannot delete your own account" });
        await _users.DeleteAsync(user);
        return Ok(new { message = "User deleted" });
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task<User?> CurrentUserAsync()
    {
        var email = User.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrEmpty(email)) return null;
        return await _users.FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
    }

    private static object MapUser(User u) => new
    {
        u.Id,
        u.FullName,
        u.Email,
        u.Role,
        u.IsActive,
        u.CreatedOn,
        u.LastLoginOn
    };
}

public record UpdateProfileRequest(string FullName);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record CreateUserRequest(string FullName, string Email, string Password, string? Role);
public record AdminUpdateUserRequest(string FullName, string? Role, bool IsActive);
public record ResetPasswordRequest(string NewPassword);
