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
using Knowz.SelfHosted.Infrastructure.Data.Entities;
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

        // Check if admin exists with wrong role (legacy enum values from before SwapUserRoleValues)
        var existingAdmin = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == _options.SuperAdminUsername);
        if (existingAdmin != null)
        {
            _logger.LogWarning(
                "User '{Username}' exists with Role={Role} but no SuperAdmin found. Fixing role.",
                existingAdmin.Username, existingAdmin.Role);
            existingAdmin.Role = UserRole.SuperAdmin;
            await _db.SaveChangesAsync();
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

        // Create tenant membership for the SuperAdmin
        var membership = new UserTenantMembership
        {
            UserId = superAdmin.Id,
            TenantId = defaultTenant.Id,
            Role = UserRole.SuperAdmin,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _db.UserTenantMemberships.Add(membership);
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

    public async Task<MultiTenantLoginResult> MultiTenantLoginAsync(string username, string password)
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

        // Check tenant memberships
        var memberships = await _db.UserTenantMemberships
            .Where(m => m.UserId == user.Id && m.IsActive)
            .Include(m => m.Tenant)
            .ToListAsync();

        // 0 memberships (pre-migration legacy user): fall back to User.TenantId + User.Role
        if (memberships.Count == 0)
        {
            _logger.LogInformation("User {Username} has no tenant memberships, using legacy home tenant", username);
            var authResult = GenerateAuthResult(user);
            return new MultiTenantLoginResult
            {
                Token = authResult.Token,
                ExpiresAt = authResult.ExpiresAt,
                User = authResult.User,
                RequiresTenantSelection = false,
                UserId = user.Id
            };
        }

        // 1 active membership: auto-select
        if (memberships.Count == 1)
        {
            var membership = memberships[0];
            _logger.LogInformation("User {Username} has single tenant membership, auto-selecting tenant {TenantId}", username, membership.TenantId);
            var authResult = GenerateAuthResultForMembership(user, membership.TenantId, membership.Tenant.Name, membership.Role);
            return new MultiTenantLoginResult
            {
                Token = authResult.Token,
                ExpiresAt = authResult.ExpiresAt,
                User = authResult.User,
                RequiresTenantSelection = false,
                UserId = user.Id
            };
        }

        // 2+ active memberships: require tenant selection
        _logger.LogInformation("User {Username} has {Count} tenant memberships, requiring selection", username, memberships.Count);
        return new MultiTenantLoginResult
        {
            Token = string.Empty,
            ExpiresAt = null,
            User = null,
            RequiresTenantSelection = true,
            UserId = user.Id,
            AvailableTenants = memberships.Select(m => new TenantMembershipDto
            {
                TenantId = m.TenantId,
                TenantName = m.Tenant.Name,
                TenantSlug = m.Tenant.Slug,
                Role = m.Role,
                IsActive = m.IsActive
            }).ToList()
        };
    }

    public async Task<AuthResult> SelectTenantAsync(Guid userId, Guid tenantId)
    {
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User not found or inactive.");
        }

        var membership = await _db.UserTenantMemberships
            .Include(m => m.Tenant)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == tenantId && m.IsActive);

        if (membership is null)
        {
            throw new UnauthorizedAccessException($"User does not have an active membership in the requested tenant.");
        }

        // Update last login time
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return GenerateAuthResultForMembership(user, membership.TenantId, membership.Tenant.Name, membership.Role);
    }

    public async Task<AuthResult> SwitchTenantAsync(Guid userId, Guid newTenantId)
    {
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null || !user.IsActive)
        {
            throw new UnauthorizedAccessException("User not found or inactive.");
        }

        var membership = await _db.UserTenantMemberships
            .Include(m => m.Tenant)
            .FirstOrDefaultAsync(m => m.UserId == userId && m.TenantId == newTenantId && m.IsActive);

        if (membership is null)
        {
            throw new UnauthorizedAccessException($"User does not have an active membership in the requested tenant.");
        }

        return GenerateAuthResultForMembership(user, membership.TenantId, membership.Tenant.Name, membership.Role);
    }

    public async Task<List<TenantMembershipDto>> GetUserTenantsAsync(Guid userId)
    {
        var memberships = await _db.UserTenantMemberships
            .Where(m => m.UserId == userId && m.IsActive)
            .Include(m => m.Tenant)
            .ToListAsync();

        return memberships.Select(m => new TenantMembershipDto
        {
            TenantId = m.TenantId,
            TenantName = m.Tenant.Name,
            TenantSlug = m.Tenant.Slug,
            Role = m.Role,
            IsActive = m.IsActive
        }).ToList();
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

    private AuthResult GenerateAuthResultForMembership(User user, Guid tenantId, string tenantName, UserRole role)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.JwtExpirationMinutes);
        var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
        var token = JwtTokenHelper.GenerateToken(user.Id, displayName, tenantId, role, expiresAt, _options.JwtSecret, _options.JwtIssuer, _logger);

        return new AuthResult
        {
            Token = token,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Role = role,
                TenantId = tenantId,
                TenantName = tenantName,
                IsActive = user.IsActive,
                ApiKey = user.ApiKey,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            }
        };
    }

    private string GenerateJwtToken(User user, DateTime expiresAt) =>
        JwtTokenHelper.GenerateToken(user, expiresAt, _options.JwtSecret, _options.JwtIssuer, _logger);

}
