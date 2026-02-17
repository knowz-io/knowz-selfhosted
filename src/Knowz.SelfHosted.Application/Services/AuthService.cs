using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Knowz.Core.Configuration;
using Knowz.Core.Entities;
using Knowz.Core.Enums;
using Knowz.SelfHosted.Application.Extensions;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Application.Models;
using Knowz.SelfHosted.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Knowz.SelfHosted.Application.Services;

/// <summary>
/// Handles authentication: login, API key validation, JWT generation, password hashing, and SuperAdmin seeding.
/// </summary>
public class AuthService : IAuthService
{
    private readonly SelfHostedDbContext _db;
    private readonly SelfHostedOptions _options;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        SelfHostedDbContext db,
        IOptions<SelfHostedOptions> options,
        ILogger<AuthService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AuthResult> LoginAsync(string username, string password)
    {
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user is null)
        {
            _logger.LogWarning("Login attempt for non-existent user: {Username}", username);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login attempt for inactive user: {Username}", username);
            throw new UnauthorizedAccessException("User account is inactive.");
        }

        if (!VerifyPassword(password, user.PasswordHash))
        {
            _logger.LogWarning("Invalid password for user: {Username}", username);
            throw new UnauthorizedAccessException("Invalid username or password.");
        }

        // Update last login time
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return GenerateAuthResult(user);
    }

    public async Task<AuthResult?> ValidateApiKeyAsync(string apiKey)
    {
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.ApiKey == apiKey);

        if (user is null || !user.IsActive)
        {
            return null;
        }

        return GenerateAuthResult(user);
    }

    public async Task<UserDto?> GetCurrentUserAsync(Guid userId)
    {
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            return null;
        }

        return user.ToDto();
    }

    public async Task EnsureSuperAdminExistsAsync()
    {
        var hasSuperAdmin = await _db.Users.AnyAsync(u => u.Role == UserRole.SuperAdmin);
        if (hasSuperAdmin)
        {
            _logger.LogInformation("SuperAdmin user already exists. Skipping seed.");
            return;
        }

        _logger.LogInformation("No SuperAdmin found. Creating default SuperAdmin user.");

        // Create or find default tenant
        var defaultTenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == "default");
        if (defaultTenant is null)
        {
            defaultTenant = new Tenant
            {
                Name = "Default",
                Slug = "default",
                Description = "Default tenant created during initial setup",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Tenants.Add(defaultTenant);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Created default tenant: {TenantId}", defaultTenant.Id);
        }

        var superAdmin = new User
        {
            TenantId = defaultTenant.Id,
            Username = _options.SuperAdminUsername,
            PasswordHash = HashPassword(_options.SuperAdminPassword),
            DisplayName = "Super Administrator",
            Role = UserRole.SuperAdmin,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(superAdmin);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created SuperAdmin user: {Username} in tenant: {TenantName}",
            superAdmin.Username, defaultTenant.Name);
    }

    public string HashPassword(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }

    private AuthResult GenerateAuthResult(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.JwtExpirationMinutes);
        var token = GenerateJwtToken(user, expiresAt);

        return new AuthResult
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = user.ToDto()
        };
    }

    private string GenerateJwtToken(User user, DateTime expiresAt) =>
        JwtTokenHelper.GenerateToken(user, expiresAt, _options.JwtSecret, _options.JwtIssuer, _logger);

}
