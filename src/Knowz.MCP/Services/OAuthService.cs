using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace Knowz.MCP.Services;

public interface IOAuthService
{
    AuthorizationRequest CreateAuthorizationRequest(
        string clientId,
        string redirectUri,
        string scope,
        string state,
        string codeChallenge,
        string codeChallengeMethod);

    AuthorizationRequest? GetAuthorizationRequest(string requestId);

    string CompleteAuthorization(string requestId, string apiKey);

    TokenResult? ExchangeCode(string code, string codeVerifier, string redirectUri);

    void CleanupExpired();
}

public class OAuthService : IOAuthService
{
    private readonly ConcurrentDictionary<string, AuthorizationRequest> _pendingRequests = new();
    private readonly ConcurrentDictionary<string, AuthorizationCode> _authCodes = new();
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(ILogger<OAuthService> logger)
    {
        _logger = logger;
    }

    public AuthorizationRequest CreateAuthorizationRequest(
        string clientId,
        string redirectUri,
        string scope,
        string state,
        string codeChallenge,
        string codeChallengeMethod)
    {
        var request = new AuthorizationRequest
        {
            RequestId = Guid.NewGuid().ToString("N"),
            ClientId = clientId,
            RedirectUri = redirectUri,
            Scope = scope,
            State = state,
            CodeChallenge = codeChallenge,
            CodeChallengeMethod = codeChallengeMethod,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10)
        };

        _pendingRequests[request.RequestId] = request;
        _logger.LogInformation("Created authorization request {RequestId} for client {ClientId}",
            request.RequestId, clientId);

        return request;
    }

    public AuthorizationRequest? GetAuthorizationRequest(string requestId)
    {
        if (_pendingRequests.TryGetValue(requestId, out var request))
        {
            if (request.ExpiresAt > DateTime.UtcNow)
            {
                return request;
            }
            _pendingRequests.TryRemove(requestId, out _);
        }
        return null;
    }

    public string CompleteAuthorization(string requestId, string apiKey)
    {
        if (!_pendingRequests.TryRemove(requestId, out var request))
        {
            throw new InvalidOperationException("Authorization request not found or expired");
        }

        var code = GenerateSecureCode();
        var authCode = new AuthorizationCode
        {
            Code = code,
            ClientId = request.ClientId,
            RedirectUri = request.RedirectUri,
            Scope = request.Scope,
            CodeChallenge = request.CodeChallenge,
            CodeChallengeMethod = request.CodeChallengeMethod,
            ApiKey = apiKey,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };

        _authCodes[code] = authCode;
        _logger.LogInformation("Generated authorization code for client {ClientId}", request.ClientId);

        return code;
    }

    public TokenResult? ExchangeCode(string code, string codeVerifier, string redirectUri)
    {
        if (!_authCodes.TryRemove(code, out var authCode))
        {
            _logger.LogWarning("Authorization code not found or already used");
            return null;
        }

        if (authCode.ExpiresAt < DateTime.UtcNow)
        {
            _logger.LogWarning("Authorization code expired");
            return null;
        }

        if (authCode.RedirectUri != redirectUri)
        {
            _logger.LogWarning("Redirect URI mismatch");
            return null;
        }

        if (!ValidatePkce(authCode.CodeChallenge, authCode.CodeChallengeMethod, codeVerifier))
        {
            _logger.LogWarning("PKCE validation failed");
            return null;
        }

        _logger.LogInformation("Successfully exchanged authorization code for client {ClientId}",
            authCode.ClientId);

        return new TokenResult
        {
            AccessToken = authCode.ApiKey,
            TokenType = "Bearer",
            ExpiresIn = 3600,
            Scope = authCode.Scope
        };
    }

    public void CleanupExpired()
    {
        var now = DateTime.UtcNow;

        foreach (var kvp in _pendingRequests)
        {
            if (kvp.Value.ExpiresAt < now)
            {
                _pendingRequests.TryRemove(kvp.Key, out _);
            }
        }

        foreach (var kvp in _authCodes)
        {
            if (kvp.Value.ExpiresAt < now)
            {
                _authCodes.TryRemove(kvp.Key, out _);
            }
        }
    }

    private static string GenerateSecureCode()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .Replace("=", "");
    }

    private static bool ValidatePkce(string codeChallenge, string method, string codeVerifier)
    {
        if (string.IsNullOrEmpty(codeVerifier))
            return false;

        if (method == "S256")
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
            var computed = Convert.ToBase64String(hash)
                .Replace("+", "-")
                .Replace("/", "_")
                .TrimEnd('=');
            return computed == codeChallenge;
        }

        if (method == "plain")
        {
            return codeVerifier == codeChallenge;
        }

        return false;
    }
}

public class AuthorizationRequest
{
    public required string RequestId { get; init; }
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
    public required string Scope { get; init; }
    public required string State { get; init; }
    public required string CodeChallenge { get; init; }
    public required string CodeChallengeMethod { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
}

public class AuthorizationCode
{
    public required string Code { get; init; }
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
    public required string Scope { get; init; }
    public required string CodeChallenge { get; init; }
    public required string CodeChallengeMethod { get; init; }
    public required string ApiKey { get; init; }
    public required DateTime CreatedAt { get; init; }
    public required DateTime ExpiresAt { get; init; }
}

public class TokenResult
{
    public required string AccessToken { get; init; }
    public required string TokenType { get; init; }
    public required int ExpiresIn { get; init; }
    public required string Scope { get; init; }
}
