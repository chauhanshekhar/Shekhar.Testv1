using Trading.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using System.Security.Cryptography;
using Trading.API.Services;
using Trading.Application.Interfaces;
using Trading.Domain.Entities;
using Trading.Infrastructure.Repositories;

namespace Trading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly JwtService _jwt;
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;
    private readonly IWebHostEnvironment _env;

    public AuthController(JwtService jwt, IRepository<User> users, IUnitOfWork uow, IWebHostEnvironment env)
    {
        _jwt = jwt;
        _users = users;
        _uow = uow;
        _env = env;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password required" });

        var user = await _users.FirstOrDefaultAsync(u =>
            u.Email.ToLower() == request.Email.ToLower() && u.IsActive);

        if (user is null)
            return Unauthorized(new { message = "Invalid credentials" });

        var verification = PasswordSecurity.Verify(user, request.Password);
        if (verification == PasswordVerificationResult.Failed)
            return Unauthorized(new { message = "Invalid credentials" });

        user.LastLoginOn = IstClock.Now;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
            user.PasswordHash = PasswordSecurity.Hash(user, request.Password);
        await _users.UpdateAsync(user);

        var token = _jwt.GenerateToken(user.Email, user.Id, user.FullName, user.Role);
        return Ok(new { token, email = user.Email, name = user.FullName, role = user.Role });
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin,Super Admin")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.FullName) ||
            string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Full name, email and password are required" });

        if (request.Password.Length < 8)
            return BadRequest(new { message = "Password must be at least 8 characters" });

        var existing = await _users.FirstOrDefaultAsync(u => u.Email.ToLower() == request.Email.ToLower());
        if (existing != null)
            return BadRequest(new { message = "Email already registered" });

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            IsActive = true,
            CreatedOn = IstClock.Now
        };
        user.PasswordHash = PasswordSecurity.Hash(user, request.Password);

        await _users.AddAsync(user);

        var token = _jwt.GenerateToken(user.Email, user.Id, user.FullName, user.Role);
        return Ok(new { token, email = user.Email, name = user.FullName, role = user.Role });
    }

    // Seed a default admin user if no users exist (development helper).
    [HttpPost("seed")]
    public async Task<IActionResult> Seed()
    {
        if (!_env.IsDevelopment())
            return NotFound();

        var anyUser = await _users.FirstOrDefaultAsync(_ => true);
        if (anyUser != null)
            return Ok(new { message = "Users already exist" });

        var temporaryPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18));
        var admin = new User
        {
            FullName = "Admin Trader",
            Email = "admin@trading.com",
            Role = "Admin",
            IsActive = true,
            CreatedOn = IstClock.Now
        };
        admin.PasswordHash = PasswordSecurity.Hash(admin, temporaryPassword);

        await _users.AddAsync(admin);

        return Ok(new { message = "Default development user created", email = admin.Email, temporaryPassword });
    }
}

public record LoginRequest(string Email, string Password);
public record RegisterRequest(string FullName, string Email, string Password);
