using FluentAssertions;
using Knowz.MCP.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Knowz.MCP.Tests.Services;

/// <summary>
/// Tests for FIX_OAuthTokenExpiry — verifies expires_in changed from 3600 to 604800 (7 days).
/// </summary>
public class OAuthTokenExpiryTests
{
    private readonly OAuthService _service;

    public OAuthTokenExpiryTests()
    {
        var cacheMock = CreateWorkingCacheMock();
        var logger = new Mock<ILogger<OAuthService>>();
        _service = new OAuthService(cacheMock.Object, logger.Object);
    }

    // VERIFY-4: OAuthService.TokenExpirySeconds constant equals 604800
    [Fact]
    public void TokenExpirySeconds_Equals_604800()
    {
        OAuthService.TokenExpirySeconds.Should().Be(604800);
    }

    // VERIFY-1: authorization_code grant returns expires_in: 604800
    [Fact]
    public void ExchangeCode_Returns_ExpiresIn_604800()
    {
        var request = _service.CreateAuthorizationRequest(
            "test-client", "http://localhost:8080/callback", "mcp:read",
            "test-state", CreateS256Challenge("test-verifier"), "S256");

        var code = _service.CompleteAuthorization(request.RequestId, "ukz_test_api_key_12345678");

        var result = _service.ExchangeCode(code, "test-verifier", "http://localhost:8080/callback");

        result.Should().NotBeNull();
        result!.ExpiresIn.Should().Be(604800);
    }

    // VERIFY-3: access_token value is still the API key (no regression)
    [Fact]
    public void ExchangeCode_AccessToken_Is_ApiKey()
    {
        var apiKey = "ukz_test_api_key_12345678";
        var request = _service.CreateAuthorizationRequest(
            "test-client", "http://localhost:8080/callback", "mcp:read",
            "test-state", CreateS256Challenge("test-verifier"), "S256");

        var code = _service.CompleteAuthorization(request.RequestId, apiKey);

        var result = _service.ExchangeCode(code, "test-verifier", "http://localhost:8080/callback");

        result.Should().NotBeNull();
        result!.AccessToken.Should().Be(apiKey);
    }

    private static Mock<IDistributedCache> CreateWorkingCacheMock()
    {
        var store = new Dictionary<string, byte[]>();
        var mock = new Mock<IDistributedCache>();

        mock.Setup(c => c.Set(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>()))
            .Callback<string, byte[], DistributedCacheEntryOptions>((key, value, _) => store[key] = value);

        mock.Setup(c => c.Get(It.IsAny<string>()))
            .Returns<string>(key => store.TryGetValue(key, out var val) ? val : null);

        mock.Setup(c => c.Remove(It.IsAny<string>()))
            .Callback<string>(key => store.Remove(key));

        return mock;
    }

    private static string CreateS256Challenge(string verifier)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hash = sha256.ComputeHash(System.Text.Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
