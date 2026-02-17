using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Knowz.Core.Configuration;
using Knowz.SelfHosted.Application.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Knowz.SelfHosted.API.Middleware;

/// <summary>
/// Dual authentication middleware supporting JWT Bearer tokens, per-user API keys (ksh_ prefix),
/// and legacy single API key for backward compatibility.
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly SelfHostedOptions _options;
    private readonly ILogger<AuthenticationMiddleware> _logger;

    private static readonly string[] PublicPathPrefixes = ["/healthz", "/swagger", "/index.html", "/api/v1/auth/login", "/api/v1/auth/sso", "/api/v1/internal/"];

    public AuthenticationMiddleware(
        RequestDelegate next,
        IOptions<SelfHostedOptions> options,
        ILogger<AuthenticationMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? "";

        // Always allow public paths and static files
        if (IsPublicPath(path) || IsStaticFile(path))
        {
            await _next(context);
            return;
        }

        // Allow GET requests for non-API paths (SPA routes that fall back to index.html)
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
            context.Request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Try to authenticate via JWT or API key
        var authenticated = await TryAuthenticateJwt(context)
                         || await TryAuthenticateUserApiKey(context)
                         || TryAuthenticateLegacyApiKey(context);

        if (authenticated)
        {
            await _next(context);
            return;
        }

        // No valid auth provided
        context.Response.StatusCode = 401;
        await context.Response.WriteAsJsonAsync(new
        {
            error = "Authentication required. Provide a JWT Bearer token, X-Api-Key header, or Authorization: Bearer header."
        });
    }

    /// <summary>
    /// Tries to authenticate using a JWT Bearer token from the Authorization header.
    /// </summary>
    private Task<bool> TryAuthenticateJwt(HttpContext context)
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(false);
        }

        var token = authHeader[7..].Trim();

        // If it starts with ksh_, it's a user API key, not a JWT
        if (token.StartsWith("ksh_", StringComparison.Ordinal))
        {
            return Task.FromResult(false);
        }

        // Don't attempt JWT validation if no JwtSecret is configured
        if (string.IsNullOrWhiteSpace(_options.JwtSecret) || _options.JwtSecret.Length < 32)
        {
            return Task.FromResult(false);
        }

        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtSecret));
            var tokenHandler = new JwtSecurityTokenHandler();

            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.JwtIssuer,
                ValidateAudience = true,
                ValidAudience = _options.JwtIssuer,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            context.User = principal;
            return Task.FromResult(true);
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning("JWT validation failed: {Error}", ex.Message);
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Tries to authenticate using a per-user API key (ksh_ prefix) via X-Api-Key header
    /// or Authorization: Bearer with ksh_ prefix.
    /// </summary>
    private async Task<bool> TryAuthenticateUserApiKey(HttpContext context)
    {
        var apiKey = ApiKeyExtractor.Extract(context);

        if (string.IsNullOrWhiteSpace(apiKey))
            return false;

        // Only handle ksh_ prefixed keys here
        if (!apiKey.StartsWith("ksh_", StringComparison.Ordinal))
            return false;

        try
        {
            var authService = context.RequestServices.GetRequiredService<IAuthService>();
            var result = await authService.ValidateApiKeyAsync(apiKey);

            if (result is null)
            {
                _logger.LogWarning("Invalid user API key from {RemoteIp}", context.Connection.RemoteIpAddress);
                return false;
            }

            // Set the user principal from the JWT claims in the auth result
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                string.IsNullOrWhiteSpace(_options.JwtSecret) || _options.JwtSecret.Length < 32
                    ? "dev-fallback-secret-key-must-be-at-least-32-chars!!"
                    : _options.JwtSecret));

            var principal = tokenHandler.ValidateToken(result.Token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.JwtIssuer,
                ValidateAudience = true,
                ValidAudience = _options.JwtIssuer,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ClockSkew = TimeSpan.FromMinutes(1)
            }, out _);

            context.User = principal;
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("User API key validation error: {Error}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Legacy single API key authentication for backward compatibility.
    /// If SelfHostedOptions.ApiKey is set AND no JWT/user-API-key was provided,
    /// checks the provided key against the configured global key.
    /// </summary>
    private bool TryAuthenticateLegacyApiKey(HttpContext context)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            return false;

        var providedKey = ApiKeyExtractor.Extract(context);
        if (string.IsNullOrWhiteSpace(providedKey))
            return false;

        // Don't handle ksh_ keys as legacy
        if (providedKey.StartsWith("ksh_", StringComparison.Ordinal))
            return false;

        var configuredBytes = Encoding.UTF8.GetBytes(_options.ApiKey);
        var providedBytes = Encoding.UTF8.GetBytes(providedKey);

        if (configuredBytes.Length != providedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(configuredBytes, providedBytes))
        {
            _logger.LogWarning("Invalid legacy API key from {RemoteIp}", context.Connection.RemoteIpAddress);
            return false;
        }

        // Set a minimal identity for legacy API key users
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "api-key-user"),
            new Claim(ClaimTypes.Role, "SuperAdmin"),
            new Claim("role", "SuperAdmin")
        };
        var identity = new ClaimsIdentity(claims, "ApiKey");
        context.User = new ClaimsPrincipal(identity);

        return true;
    }

    private static bool IsPublicPath(string path) =>
        PublicPathPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    private static bool IsStaticFile(string path) =>
        path.Contains('.') && !path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
}
