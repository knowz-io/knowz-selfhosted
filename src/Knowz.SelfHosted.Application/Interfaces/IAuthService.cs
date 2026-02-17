using Knowz.SelfHosted.Application.Models;

namespace Knowz.SelfHosted.Application.Interfaces;

/// <summary>
/// Handles authentication: login, API key validation, JWT generation, password hashing, and SuperAdmin seeding.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Authenticates a user by username and password.
    /// </summary>
    Task<AuthResult> LoginAsync(string username, string password);

    /// <summary>
    /// Validates an API key and returns an AuthResult if valid.
    /// </summary>
    Task<AuthResult?> ValidateApiKeyAsync(string apiKey);

    /// <summary>
    /// Gets a user DTO by user ID.
    /// </summary>
    Task<UserDto?> GetCurrentUserAsync(Guid userId);

    /// <summary>
    /// Ensures a SuperAdmin user exists. Called at application startup.
    /// Creates one using SelfHostedOptions defaults if none exists.
    /// </summary>
    Task EnsureSuperAdminExistsAsync();

    /// <summary>
    /// Hashes a plaintext password using BCrypt.
    /// </summary>
    string HashPassword(string password);

    /// <summary>
    /// Verifies a plaintext password against a BCrypt hash.
    /// </summary>
    bool VerifyPassword(string password, string hash);
}
