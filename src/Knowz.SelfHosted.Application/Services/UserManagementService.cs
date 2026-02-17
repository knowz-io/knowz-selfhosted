using System.Security.Cryptography;
using Knowz.Core.Entities;
using Knowz.SelfHosted.Application.Extensions;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Application.Models;
using Knowz.SelfHosted.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Knowz.SelfHosted.Application.Services;

/// <summary>
/// User CRUD operations with API key generation and password reset for SuperAdmin management.
/// </summary>
public class UserManagementService : IUserManagementService
{
    private readonly SelfHostedDbContext _db;
    private readonly IAuthService _authService;
    private readonly ILogger<UserManagementService> _logger;

    private const string ApiKeyPrefix = "ksh_";
    private const int ApiKeyRandomLength = 32;

    public UserManagementService(
        SelfHostedDbContext db,
        IAuthService authService,
        ILogger<UserManagementService> logger)
    {
        _db = db;
        _authService = authService;
        _logger = logger;
    }

    public async Task<List<UserDto>> ListUsersAsync(Guid? tenantId = null)
    {
        var query = _db.Users.Include(u => u.Tenant).AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(u => u.TenantId == tenantId.Value);

        return await query
            .OrderBy(u => u.Username)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Username = u.Username,
                Email = u.Email,
                DisplayName = u.DisplayName,
                Role = u.Role,
                TenantId = u.TenantId,
                TenantName = u.Tenant.Name,
                IsActive = u.IsActive,
                ApiKey = u.ApiKey,
                CreatedAt = u.CreatedAt,
                LastLoginAt = u.LastLoginAt
            })
            .ToListAsync();
    }

    public async Task<UserDto?> GetUserAsync(Guid id)
    {
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
            return null;

        return user.ToDto();
    }

    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        // Verify tenant exists
        var tenant = await _db.Tenants.FindAsync(request.TenantId);
        if (tenant is null)
            throw new KeyNotFoundException($"Tenant with ID '{request.TenantId}' not found.");

        // Check for duplicate username
        var usernameExists = await _db.Users.AnyAsync(u => u.Username == request.Username);
        if (usernameExists)
            throw new InvalidOperationException($"A user with username '{request.Username}' already exists.");

        var user = new User
        {
            TenantId = request.TenantId,
            Username = request.Username,
            PasswordHash = _authService.HashPassword(request.Password),
            Email = request.Email,
            DisplayName = request.DisplayName,
            Role = request.Role,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Created user: {Username} in tenant: {TenantId}", user.Username, user.TenantId);

        // Reload with tenant for DTO mapping
        user.Tenant = tenant;
        return user.ToDto();
    }

    public async Task<UserDto> UpdateUserAsync(Guid id, UpdateUserRequest request)
    {
        var user = await _db.Users
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null)
            throw new KeyNotFoundException($"User with ID '{id}' not found.");

        if (request.Email is not null)
            user.Email = request.Email;

        if (request.DisplayName is not null)
            user.DisplayName = request.DisplayName;

        if (request.Role.HasValue)
            user.Role = request.Role.Value;

        if (request.IsActive.HasValue)
            user.IsActive = request.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Updated user: {UserId}", id);

        return user.ToDto();
    }

    public async Task DeleteUserAsync(Guid id)
    {
        var user = await _db.Users.FindAsync(id);

        if (user is null)
            throw new KeyNotFoundException($"User with ID '{id}' not found.");

        _db.Users.Remove(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Deleted user: {UserId} ({Username})", id, user.Username);
    }

    public async Task<string> GenerateApiKeyAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);

        if (user is null)
            throw new KeyNotFoundException($"User with ID '{userId}' not found.");

        var apiKey = GenerateApiKey();
        user.ApiKey = apiKey;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Generated new API key for user: {UserId}", userId);

        return apiKey;
    }

    public async Task RevokeApiKeyAsync(Guid userId)
    {
        var user = await _db.Users.FindAsync(userId);

        if (user is null)
            throw new KeyNotFoundException($"User with ID '{userId}' not found.");

        user.ApiKey = null;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Revoked API key for user: {UserId}", userId);
    }

    public async Task<string> ResetPasswordAsync(Guid userId, string newPassword)
    {
        var user = await _db.Users.FindAsync(userId);

        if (user is null)
            throw new KeyNotFoundException($"User with ID '{userId}' not found.");

        user.PasswordHash = _authService.HashPassword(newPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Password reset for user: {UserId}", userId);

        return "Password reset successfully";
    }

    private static string GenerateApiKey()
    {
        const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var randomBytes = RandomNumberGenerator.GetBytes(ApiKeyRandomLength);
        var result = new char[ApiKeyRandomLength];

        for (int i = 0; i < ApiKeyRandomLength; i++)
        {
            result[i] = chars[randomBytes[i] % chars.Length];
        }

        return $"{ApiKeyPrefix}{new string(result)}";
    }

}
