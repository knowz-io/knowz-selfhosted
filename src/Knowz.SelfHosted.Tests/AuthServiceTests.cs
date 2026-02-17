using Knowz.Core.Configuration;
using Knowz.Core.Entities;
using Knowz.Core.Enums;
using Knowz.SelfHosted.Application.Interfaces;
using Knowz.SelfHosted.Application.Services;
using Knowz.SelfHosted.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Knowz.SelfHosted.Tests;

public class AuthServiceTests : IDisposable
{
    private readonly SelfHostedDbContext _db;
    private readonly IAuthService _authService;
    private readonly SelfHostedOptions _options;
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public AuthServiceTests()
    {
        var dbOptions = new DbContextOptionsBuilder<SelfHostedDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _options = new SelfHostedOptions
        {
            TenantId = TenantId,
            SuperAdminUsername = "admin",
            SuperAdminPassword = "changeme",
            JwtSecret = "this-is-a-test-secret-key-at-least-32-characters",
            JwtExpirationMinutes = 60,
            JwtIssuer = "test-issuer"
        };

        var selfHostedOptions = Options.Create(_options);
        var tenantProvider = Substitute.For<Knowz.Core.Interfaces.ITenantProvider>();
        tenantProvider.TenantId.Returns(TenantId);
        _db = new SelfHostedDbContext(dbOptions, tenantProvider);
        var logger = Substitute.For<ILogger<AuthService>>();

        _authService = new AuthService(_db, selfHostedOptions, logger);
    }

    public void Dispose()
    {
        _db.Database.EnsureDeleted();
        _db.Dispose();
    }

    // --- Password Hashing ---

    [Fact]
    public void Should_HashPassword_WhenGivenPlaintext()
    {
        var hash = _authService.HashPassword("testpassword");

        Assert.NotNull(hash);
        Assert.NotEmpty(hash);
        Assert.NotEqual("testpassword", hash);
    }

    [Fact]
    public void Should_VerifyPassword_WhenHashMatches()
    {
        var hash = _authService.HashPassword("mypassword");

        Assert.True(_authService.VerifyPassword("mypassword", hash));
    }

    [Fact]
    public void Should_RejectPassword_WhenHashDoesNotMatch()
    {
        var hash = _authService.HashPassword("mypassword");

        Assert.False(_authService.VerifyPassword("wrongpassword", hash));
    }

    [Fact]
    public void Should_ProduceDifferentHashes_ForSamePassword()
    {
        var hash1 = _authService.HashPassword("samepassword");
        var hash2 = _authService.HashPassword("samepassword");

        Assert.NotEqual(hash1, hash2); // BCrypt salting
    }

    // --- EnsureSuperAdminExistsAsync ---

    [Fact]
    public async Task Should_CreateSuperAdmin_WhenNoneExists()
    {
        await _authService.EnsureSuperAdminExistsAsync();

        var users = await _db.Users.ToListAsync();
        Assert.Single(users);
        Assert.Equal("admin", users[0].Username);
        Assert.Equal(UserRole.SuperAdmin, users[0].Role);
        Assert.True(users[0].IsActive);
    }

    [Fact]
    public async Task Should_CreateDefaultTenant_WhenSeedingSuperAdmin()
    {
        await _authService.EnsureSuperAdminExistsAsync();

        var tenants = await _db.Tenants.ToListAsync();
        Assert.Single(tenants);
        Assert.Equal("Default", tenants[0].Name);
        Assert.Equal("default", tenants[0].Slug);
    }

    [Fact]
    public async Task Should_NotCreateDuplicate_WhenSuperAdminAlreadyExists()
    {
        await _authService.EnsureSuperAdminExistsAsync();
        await _authService.EnsureSuperAdminExistsAsync(); // Call twice

        var users = await _db.Users.Where(u => u.Role == UserRole.SuperAdmin).ToListAsync();
        Assert.Single(users);
    }

    [Fact]
    public async Task Should_HashSuperAdminPassword_NotStorePlaintext()
    {
        await _authService.EnsureSuperAdminExistsAsync();

        var user = await _db.Users.FirstAsync();
        Assert.NotEqual("changeme", user.PasswordHash);
        Assert.True(_authService.VerifyPassword("changeme", user.PasswordHash));
    }

    // --- LoginAsync ---

    [Fact]
    public async Task Should_ReturnAuthResult_WhenCredentialsValid()
    {
        await _authService.EnsureSuperAdminExistsAsync();

        var result = await _authService.LoginAsync("admin", "changeme");

        Assert.NotNull(result);
        Assert.NotEmpty(result.Token);
        Assert.True(result.ExpiresAt > DateTime.UtcNow);
        Assert.Equal("admin", result.User.Username);
        Assert.Equal(UserRole.SuperAdmin, result.User.Role);
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenUsernameNotFound()
    {
        await _authService.EnsureSuperAdminExistsAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _authService.LoginAsync("nonexistent", "password"));
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenPasswordWrong()
    {
        await _authService.EnsureSuperAdminExistsAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _authService.LoginAsync("admin", "wrongpassword"));
    }

    [Fact]
    public async Task Should_ThrowUnauthorized_WhenUserInactive()
    {
        await _authService.EnsureSuperAdminExistsAsync();
        var user = await _db.Users.FirstAsync();
        user.IsActive = false;
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _authService.LoginAsync("admin", "changeme"));
    }

    [Fact]
    public async Task Should_UpdateLastLoginAt_OnSuccessfulLogin()
    {
        await _authService.EnsureSuperAdminExistsAsync();
        var beforeLogin = DateTime.UtcNow;

        await _authService.LoginAsync("admin", "changeme");

        var user = await _db.Users.FirstAsync();
        Assert.NotNull(user.LastLoginAt);
        Assert.True(user.LastLoginAt >= beforeLogin);
    }

    [Fact]
    public async Task Should_IncludeTenantName_InLoginResult()
    {
        await _authService.EnsureSuperAdminExistsAsync();

        var result = await _authService.LoginAsync("admin", "changeme");

        Assert.Equal("Default", result.User.TenantName);
    }

    // --- ValidateApiKeyAsync ---

    [Fact]
    public async Task Should_ReturnAuthResult_WhenApiKeyValid()
    {
        await _authService.EnsureSuperAdminExistsAsync();
        var user = await _db.Users.FirstAsync();
        user.ApiKey = "ksh_testapikey12345678901234567890";
        await _db.SaveChangesAsync();

        var result = await _authService.ValidateApiKeyAsync("ksh_testapikey12345678901234567890");

        Assert.NotNull(result);
        Assert.Equal("admin", result!.User.Username);
        Assert.NotEmpty(result.Token);
    }

    [Fact]
    public async Task Should_ReturnNull_WhenApiKeyNotFound()
    {
        await _authService.EnsureSuperAdminExistsAsync();

        var result = await _authService.ValidateApiKeyAsync("ksh_nonexistent");

        Assert.Null(result);
    }

    [Fact]
    public async Task Should_ReturnNull_WhenApiKeyUserInactive()
    {
        await _authService.EnsureSuperAdminExistsAsync();
        var user = await _db.Users.FirstAsync();
        user.ApiKey = "ksh_testapikey12345678901234567890";
        user.IsActive = false;
        await _db.SaveChangesAsync();

        var result = await _authService.ValidateApiKeyAsync("ksh_testapikey12345678901234567890");

        Assert.Null(result);
    }

    // --- GetCurrentUserAsync ---

    [Fact]
    public async Task Should_ReturnUserDto_WhenUserExists()
    {
        await _authService.EnsureSuperAdminExistsAsync();
        var user = await _db.Users.FirstAsync();

        var result = await _authService.GetCurrentUserAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.Id);
        Assert.Equal("admin", result.Username);
    }

    [Fact]
    public async Task Should_ReturnNull_WhenUserDoesNotExist()
    {
        var result = await _authService.GetCurrentUserAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // --- JWT Token Structure ---

    [Fact]
    public async Task Should_GenerateValidJwt_WithExpectedClaims()
    {
        await _authService.EnsureSuperAdminExistsAsync();

        var result = await _authService.LoginAsync("admin", "changeme");

        // Parse the JWT to verify claims
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var token = handler.ReadJwtToken(result.Token);

        Assert.Equal("test-issuer", token.Issuer);
        Assert.Contains(token.Claims, c => c.Type == "sub");
        Assert.Contains(token.Claims, c => c.Type == "name" && c.Value == "admin");
        Assert.Contains(token.Claims, c => c.Type == "role" && c.Value == "SuperAdmin");
        Assert.Contains(token.Claims, c => c.Type == "tenantId");
    }
}
