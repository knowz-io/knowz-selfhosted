using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Knowz.MCP.Services;

public class McpSSOService : IMcpSSOService
{
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<McpSSOService> _logger;
    private readonly bool _isSelfHosted;

    // In-memory SSO state store (keyed by OIDC state parameter)
    private static readonly ConcurrentDictionary<string, (McpSSOState Data, DateTime ExpiresAt)>
        _ssoStateStore = new();
    private const int StateExpirationMinutes = 10;

    // OIDC discovery cache (keyed by authority)
    private static readonly ConcurrentDictionary<string, (OpenIdConnectConfiguration Config, DateTime FetchedAt)>
        _oidcConfigCache = new();
    private const int OidcCacheTtlMinutes = 60;

    // Self-hosted SSO config cache (fetched from self-hosted API)
    private static (List<SelfHostedSSOProviderConfig> Providers, DateTime FetchedAt)? _selfHostedSSOConfigCache;
    private const int SelfHostedConfigCacheTtlMinutes = 5;

    public McpSSOService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<McpSSOService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _isSelfHosted = (_configuration["MCP:BackendMode"] ?? "proxy")
            .Equals("selfhosted", StringComparison.OrdinalIgnoreCase);
    }

    public List<McpSSOProvider> GetEnabledProviders()
    {
        if (_isSelfHosted)
            return GetSelfHostedProviders();

        // Platform mode: read from PlatformSSO:* config
        var isEnabled = _configuration.GetValue<bool>("PlatformSSO:Enabled");
        if (!isEnabled) return new List<McpSSOProvider>();

        var providers = new List<McpSSOProvider>();

        if (!string.IsNullOrEmpty(_configuration["PlatformSSO:Microsoft:ClientId"]))
            providers.Add(new McpSSOProvider { Provider = "Microsoft", DisplayName = "Sign in with Microsoft" });

        if (!string.IsNullOrEmpty(_configuration["PlatformSSO:Google:ClientId"]))
            providers.Add(new McpSSOProvider { Provider = "Google", DisplayName = "Sign in with Google" });

        return providers;
    }

    public async Task<McpSSOStartResult> StartSSOFlowAsync(string provider, string requestId, string callbackUrl)
    {
        var (clientId, authority) = GetProviderConfig(provider);
        if (string.IsNullOrEmpty(clientId))
            return new McpSSOStartResult { Success = false, ErrorMessage = $"SSO not configured for {provider}" };

        // Generate PKCE
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var state = GenerateSecureRandomString(32);
        var nonce = GenerateSecureRandomString(32);

        // Store state linking OIDC state to MCP requestId
        CleanupExpiredStates();
        _ssoStateStore[state] = (new McpSSOState
        {
            Provider = provider,
            RequestId = requestId,
            CodeVerifier = codeVerifier,
            Nonce = nonce,
            CallbackUrl = callbackUrl,
        }, DateTime.UtcNow.AddMinutes(StateExpirationMinutes));

        // Fetch OIDC discovery
        var oidcConfig = await GetOidcConfigurationAsync(authority);

        var authUrl = $"{oidcConfig.AuthorizationEndpoint}" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(callbackUrl)}" +
            $"&scope={Uri.EscapeDataString("openid email profile")}" +
            $"&state={Uri.EscapeDataString(state)}" +
            $"&nonce={Uri.EscapeDataString(nonce)}" +
            $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
            $"&code_challenge_method=S256" +
            $"&response_mode=query";

        return new McpSSOStartResult { Success = true, AuthorizationUrl = authUrl };
    }

    public async Task<McpSSOCallbackResult> HandleSSOCallbackAsync(string code, string state)
    {
        // 1. Retrieve and remove state (single-use)
        if (!_ssoStateStore.TryRemove(state, out var stateEntry) || stateEntry.ExpiresAt < DateTime.UtcNow)
            return new McpSSOCallbackResult { Success = false, ErrorMessage = "Invalid or expired SSO state" };

        var ssoState = stateEntry.Data;
        var (clientId, authority) = GetProviderConfig(ssoState.Provider);
        var clientSecret = GetClientSecret(ssoState.Provider);

        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            return new McpSSOCallbackResult { Success = false, ErrorMessage = "SSO configuration incomplete" };

        // 2. Exchange authorization code for tokens
        var oidcConfig = await GetOidcConfigurationAsync(authority);
        var httpClient = _httpClientFactory.CreateClient();

        var tokenRequestBody = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = ssoState.CallbackUrl,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["code_verifier"] = ssoState.CodeVerifier,
        };

        var tokenResponse = await httpClient.PostAsync(
            oidcConfig.TokenEndpoint,
            new FormUrlEncodedContent(tokenRequestBody));

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var error = await tokenResponse.Content.ReadAsStringAsync();
            _logger.LogWarning("MCP SSO token exchange failed for provider {Provider}: {Error}",
                ssoState.Provider, error);
            return new McpSSOCallbackResult { Success = false, ErrorMessage = "Authentication failed" };
        }

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();
        var tokenDoc = JsonDocument.Parse(tokenJson);

        if (!tokenDoc.RootElement.TryGetProperty("id_token", out var idTokenEl))
            return new McpSSOCallbackResult { Success = false, ErrorMessage = "No ID token returned" };

        // 3. Validate ID token and extract email
        var email = await ValidateAndExtractEmailAsync(
            idTokenEl.GetString()!, ssoState.Provider, clientId, authority, ssoState.Nonce);

        if (string.IsNullOrEmpty(email))
            return new McpSSOCallbackResult { Success = false, ErrorMessage = "No email found in ID token" };

        // 4. Resolve email to API key via platform internal endpoint
        var apiKey = await ResolveEmailToApiKeyAsync(email, ssoState.Provider);

        if (string.IsNullOrEmpty(apiKey))
            return new McpSSOCallbackResult
            {
                Success = false,
                ErrorMessage = "No Knowz account found for this email. Please register first."
            };

        _logger.LogInformation("MCP SSO completed for email {Email} via {Provider}", email, ssoState.Provider);

        return new McpSSOCallbackResult
        {
            Success = true,
            RequestId = ssoState.RequestId,
            ApiKey = apiKey,
            Email = email,
        };
    }

    // ==================== Private Helpers ====================

    private (string? ClientId, string Authority) GetProviderConfig(string provider)
    {
        if (_isSelfHosted)
        {
            var config = GetCachedSelfHostedConfig(provider);
            return config != null ? (config.ClientId, config.Authority) : (null, string.Empty);
        }

        return provider.ToLowerInvariant() switch
        {
            "microsoft" => (
                _configuration["PlatformSSO:Microsoft:ClientId"],
                "https://login.microsoftonline.com/common/v2.0"
            ),
            "google" => (
                _configuration["PlatformSSO:Google:ClientId"],
                "https://accounts.google.com"
            ),
            _ => (null, string.Empty)
        };
    }

    private string? GetClientSecret(string provider)
    {
        if (_isSelfHosted)
        {
            var config = GetCachedSelfHostedConfig(provider);
            return config?.ClientSecret;
        }

        return provider.ToLowerInvariant() switch
        {
            "microsoft" => _configuration["PlatformSSO:Microsoft:ClientSecret"],
            "google" => _configuration["PlatformSSO:Google:ClientSecret"],
            _ => null
        };
    }

    private async Task<OpenIdConnectConfiguration> GetOidcConfigurationAsync(string authority)
    {
        if (_oidcConfigCache.TryGetValue(authority, out var cached) &&
            (DateTime.UtcNow - cached.FetchedAt).TotalMinutes < OidcCacheTtlMinutes)
        {
            return cached.Config;
        }

        var metadataAddress = authority.TrimEnd('/') + "/.well-known/openid-configuration";
        var configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress,
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever(_httpClientFactory.CreateClient()));

        var config = await configManager.GetConfigurationAsync(CancellationToken.None);
        _oidcConfigCache[authority] = (config, DateTime.UtcNow);

        return config;
    }

    private async Task<string?> ValidateAndExtractEmailAsync(
        string idToken, string provider, string clientId, string authority, string expectedNonce)
    {
        try
        {
            var oidcConfig = await GetOidcConfigurationAsync(authority);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidAudience = clientId,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = oidcConfig.SigningKeys,
                ClockSkew = TimeSpan.FromMinutes(5),
            };

            // Microsoft "common" endpoint uses tenant-specific issuers
            if (provider.Equals("Microsoft", StringComparison.OrdinalIgnoreCase))
            {
                validationParameters.IssuerValidator = (issuer, token, parameters) =>
                {
                    if (issuer.StartsWith("https://login.microsoftonline.com/") &&
                        issuer.EndsWith("/v2.0"))
                        return issuer;
                    throw new SecurityTokenInvalidIssuerException($"Invalid issuer: {issuer}");
                };
            }
            else
            {
                validationParameters.ValidIssuer = authority;
            }

            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(idToken, validationParameters, out var validatedToken);

            var jwt = (JwtSecurityToken)validatedToken;

            // Validate nonce
            var tokenNonce = jwt.Claims.FirstOrDefault(c => c.Type == "nonce")?.Value;
            if (tokenNonce != expectedNonce)
            {
                _logger.LogWarning("MCP SSO nonce mismatch for provider {Provider}", provider);
                return null;
            }

            // Extract email from claims (try multiple claim types)
            var email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "preferred_username")?.Value
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "upn")?.Value;

            return email;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MCP SSO ID token validation failed for provider {Provider}", provider);
            return null;
        }
    }

    private async Task<string?> ResolveEmailToApiKeyAsync(string email, string provider)
    {
        var platformUrl = _configuration["Knowz:BaseUrl"]
            ?? throw new InvalidOperationException("Knowz:BaseUrl is not configured");
        var serviceKey = _configuration["MCP:ServiceKey"];

        if (string.IsNullOrEmpty(serviceKey))
        {
            _logger.LogError("MCP:ServiceKey not configured -- cannot resolve SSO email to API key");
            return null;
        }

        var httpClient = _httpClientFactory.CreateClient("McpApiClient");
        var request = new HttpRequestMessage(HttpMethod.Post, $"{platformUrl}/api/v1/internal/sso/resolve")
        {
            Content = JsonContent.Create(new { email, provider })
        };
        request.Headers.Add("X-Service-Key", serviceKey);

        try
        {
            var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Platform SSO resolve failed: {Status} {Error}",
                    response.StatusCode, errorBody);
                return null;
            }

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (body.TryGetProperty("data", out var data) &&
                data.TryGetProperty("apiKey", out var apiKeyEl))
            {
                return apiKeyEl.GetString();
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resolve SSO email to API key via platform API");
            return null;
        }
    }

    // ==================== Self-Hosted SSO Config ====================

    /// <summary>
    /// Gets enabled SSO providers from the self-hosted API's internal config endpoint.
    /// Results are cached for 5 minutes.
    /// </summary>
    private List<McpSSOProvider> GetSelfHostedProviders()
    {
        var configs = FetchSelfHostedSSOConfigAsync().GetAwaiter().GetResult();
        return configs.Select(c => new McpSSOProvider
        {
            Provider = c.Provider,
            DisplayName = c.DisplayName,
        }).ToList();
    }

    private SelfHostedSSOProviderConfig? GetCachedSelfHostedConfig(string provider)
    {
        var configs = FetchSelfHostedSSOConfigAsync().GetAwaiter().GetResult();
        return configs.FirstOrDefault(c =>
            c.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<List<SelfHostedSSOProviderConfig>> FetchSelfHostedSSOConfigAsync()
    {
        // Return cached if still valid
        if (_selfHostedSSOConfigCache.HasValue &&
            (DateTime.UtcNow - _selfHostedSSOConfigCache.Value.FetchedAt).TotalMinutes < SelfHostedConfigCacheTtlMinutes)
        {
            return _selfHostedSSOConfigCache.Value.Providers;
        }

        var baseUrl = _configuration["Knowz:BaseUrl"]
            ?? throw new InvalidOperationException("Knowz:BaseUrl is not configured");
        var serviceKey = _configuration["MCP:ServiceKey"];

        if (string.IsNullOrEmpty(serviceKey))
        {
            _logger.LogWarning("MCP:ServiceKey not configured -- cannot fetch self-hosted SSO config");
            return new List<SelfHostedSSOProviderConfig>();
        }

        try
        {
            var httpClient = _httpClientFactory.CreateClient("McpApiClient");
            var request = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/api/v1/internal/sso/config");
            request.Headers.Add("X-Service-Key", serviceKey);

            var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch self-hosted SSO config: {Status}", response.StatusCode);
                return _selfHostedSSOConfigCache?.Providers ?? new List<SelfHostedSSOProviderConfig>();
            }

            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var providers = new List<SelfHostedSSOProviderConfig>();

            if (body.TryGetProperty("data", out var data) &&
                data.TryGetProperty("providers", out var providersEl))
            {
                foreach (var p in providersEl.EnumerateArray())
                {
                    providers.Add(new SelfHostedSSOProviderConfig
                    {
                        Provider = p.GetProperty("provider").GetString() ?? "",
                        DisplayName = p.GetProperty("displayName").GetString() ?? "",
                        ClientId = p.GetProperty("clientId").GetString() ?? "",
                        ClientSecret = p.TryGetProperty("clientSecret", out var cs) ? cs.GetString() : null,
                        Authority = p.GetProperty("authority").GetString() ?? "",
                    });
                }
            }

            _selfHostedSSOConfigCache = (providers, DateTime.UtcNow);
            return providers;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch self-hosted SSO config");
            return _selfHostedSSOConfigCache?.Providers ?? new List<SelfHostedSSOProviderConfig>();
        }
    }

    private class SelfHostedSSOProviderConfig
    {
        public string Provider { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string? ClientSecret { get; set; }
        public string Authority { get; set; } = string.Empty;
    }

    // ==================== PKCE & Crypto ====================

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string GenerateCodeChallenge(string codeVerifier)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.ASCII.GetBytes(codeVerifier));
        return Convert.ToBase64String(hash)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private static string GenerateSecureRandomString(int length)
    {
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=')[..length];
    }

    private static void CleanupExpiredStates()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _ssoStateStore)
        {
            if (kvp.Value.ExpiresAt < now)
                _ssoStateStore.TryRemove(kvp.Key, out _);
        }
    }

    private class McpSSOState
    {
        public string Provider { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
        public string CodeVerifier { get; set; } = string.Empty;
        public string Nonce { get; set; } = string.Empty;
        public string CallbackUrl { get; set; } = string.Empty;
    }
}
