using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Knowz.Core.Entities;
using Knowz.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Knowz.SelfHosted.Application.Extensions;

public static class JwtTokenHelper
{
    private const string DevFallbackSecret = "dev-fallback-secret-key-must-be-at-least-32-chars!!";

    /// <summary>
    /// Generates a JWT using the user's own TenantId and Role (backward-compatible).
    /// </summary>
    public static string GenerateToken(User user, DateTime expiresAt, string jwtSecret, string jwtIssuer, ILogger logger)
    {
        var displayName = string.IsNullOrWhiteSpace(user.DisplayName) ? user.Username : user.DisplayName;
        return GenerateToken(user.Id, displayName, user.TenantId, user.Role, expiresAt, jwtSecret, jwtIssuer, logger);
    }

    /// <summary>
    /// Generates a JWT with explicit tenantId and role, allowing tokens to reflect
    /// a specific tenant membership rather than the user's home tenant.
    /// </summary>
    public static string GenerateToken(Guid userId, string displayName, Guid tenantId, UserRole role, DateTime expiresAt, string jwtSecret, string jwtIssuer, ILogger logger)
    {
        var secret = jwtSecret;
        if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
        {
            secret = DevFallbackSecret;
            logger.LogWarning("JwtSecret is not properly configured. Using fallback for development only.");
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Name, displayName),
            new Claim(ClaimTypes.Role, role.ToString()),
            new Claim("role", role.ToString()),
            new Claim("tenantId", tenantId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtIssuer,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
