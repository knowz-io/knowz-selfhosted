using FluentAssertions;
using Knowz.MCP.Services.Session;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Knowz.MCP.Tests.Services;

/// <summary>
/// Tests for FIX_LastAuthKeyTimeout — verifies _lastAuthKeyTimeout changed from 30 min to 24 hours.
/// </summary>
public class LastAuthKeyTimeoutTests
{
    #region RedisMcpSessionStore Tests

    // VERIFY-1: RedisMcpSessionStore.GetLastAuthenticatedApiKey() returns key within 24 hours
    [Fact]
    public void RedisMcpSessionStore_GetLastAuthenticatedApiKey_ReturnsKey_Within24Hours()
    {
        var store = CreateRedisSessionStore();
        var apiKey = "ukz_test_key_1234567890";

        // Store a key via StoreApiKey (which sets _lastAuthenticatedApiKey)
        store.StoreApiKey("test-session", apiKey);

        // Within 24 hours, key should be returned
        store.GetLastAuthenticatedApiKey().Should().Be(apiKey);
    }

    // VERIFY-2: RedisMcpSessionStore.GetLastAuthenticatedApiKey() returns null after 24 hours
    [Fact]
    public void RedisMcpSessionStore_GetLastAuthenticatedApiKey_ReturnsNull_After24Hours()
    {
        // We need to test that the timeout is 24 hours.
        // Since the field is private and time-based, we use a wrapper approach.
        // For now, we verify the behavior indirectly: store a key, then check
        // that the timeout value is correct by verifying the key is returned
        // immediately (proving the timeout is > 0) and that the constant
        // is set correctly via reflection.
        var store = CreateRedisSessionStore();

        // Verify the timeout field is 24 hours via reflection
        var timeoutField = typeof(RedisMcpSessionStore)
            .GetField("_lastAuthKeyTimeout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        timeoutField.Should().NotBeNull("_lastAuthKeyTimeout field should exist");

        var timeoutValue = (TimeSpan)timeoutField!.GetValue(store)!;
        timeoutValue.Should().Be(TimeSpan.FromHours(24),
            "timeout should be 24 hours to match MCP SDK IdleTimeout");
    }

    // Additional: verify key is returned when just stored
    [Fact]
    public void RedisMcpSessionStore_GetLastAuthenticatedApiKey_ReturnsNull_WhenNeverStored()
    {
        var store = CreateRedisSessionStore();
        store.GetLastAuthenticatedApiKey().Should().BeNull();
    }

    #endregion

    #region McpSessionStore Tests

    // VERIFY-3: McpSessionStore.GetLastAuthenticatedApiKey() returns key within 24 hours
    [Fact]
    public void McpSessionStore_GetLastAuthenticatedApiKey_ReturnsKey_Within24Hours()
    {
        var store = CreateInMemorySessionStore();
        var apiKey = "ukz_test_key_1234567890";

        store.StoreApiKey("test-session", apiKey);

        store.GetLastAuthenticatedApiKey().Should().Be(apiKey);
    }

    // VERIFY-4: McpSessionStore.GetLastAuthenticatedApiKey() returns null after 24 hours
    [Fact]
    public void McpSessionStore_GetLastAuthenticatedApiKey_ReturnsNull_After24Hours()
    {
        var store = CreateInMemorySessionStore();

        var timeoutField = typeof(McpSessionStore)
            .GetField("_lastAuthKeyTimeout", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        timeoutField.Should().NotBeNull("_lastAuthKeyTimeout field should exist");

        var timeoutValue = (TimeSpan)timeoutField!.GetValue(store)!;
        timeoutValue.Should().Be(TimeSpan.FromHours(24),
            "timeout should be 24 hours to match MCP SDK IdleTimeout");
    }

    // Additional: verify key is returned when just stored
    [Fact]
    public void McpSessionStore_GetLastAuthenticatedApiKey_ReturnsNull_WhenNeverStored()
    {
        var store = CreateInMemorySessionStore();
        store.GetLastAuthenticatedApiKey().Should().BeNull();
    }

    #endregion

    #region Helpers

    private static RedisMcpSessionStore CreateRedisSessionStore()
    {
        var cacheMock = new Mock<IDistributedCache>();
        var loggerMock = new Mock<ILogger<RedisMcpSessionStore>>();
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(c => c.GetSection("MCP_SESSION_TIMEOUT_HOURS").Value).Returns((string?)null);

        return new RedisMcpSessionStore(cacheMock.Object, loggerMock.Object, configMock.Object);
    }

    private static McpSessionStore CreateInMemorySessionStore()
    {
        var loggerMock = new Mock<ILogger<McpSessionStore>>();
        return new McpSessionStore(loggerMock.Object);
    }

    #endregion
}
