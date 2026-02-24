using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace Knowz.MCP.Services.Session;

/// <summary>
/// Redis-backed implementation of IMcpSessionStore that persists sessions across deployments and restarts.
/// Falls back to in-memory storage if Redis is unavailable.
/// </summary>
public class RedisMcpSessionStore : IMcpSessionStore
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisMcpSessionStore> _logger;
    private readonly TimeSpan _sessionTimeout;
    private readonly TimeSpan _lastAuthKeyTimeout = TimeSpan.FromHours(24);
    private readonly ConcurrentDictionary<string, SessionData> _fallbackStore = new();
    private bool _redisAvailable = true;

    private string? _lastAuthenticatedApiKey;
    private DateTime _lastAuthenticatedAt;

    private const string KeyPrefix = "session:";
    private const string FingerprintPrefix = "fingerprint:";
    private const string LastAuthKey = "mcp:last-auth-key";

    public RedisMcpSessionStore(IDistributedCache cache, ILogger<RedisMcpSessionStore> logger, IConfiguration configuration)
    {
        _cache = cache;
        _logger = logger;

        var timeoutHours = configuration.GetValue<int>("MCP_SESSION_TIMEOUT_HOURS", 168);
        _sessionTimeout = TimeSpan.FromHours(timeoutHours);

        _logger.LogInformation("MCP session store initialized with {TimeoutHours}h timeout", timeoutHours);
    }

    public void StoreApiKey(string sessionId, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(apiKey))
            return;

        var data = new SessionData { ApiKey = apiKey, LastActivity = DateTime.UtcNow };

        try
        {
            var json = JsonSerializer.Serialize(data);
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = _sessionTimeout
            };

            _cache.SetString(KeyPrefix + sessionId, json, options);
            _redisAvailable = true;

            _lastAuthenticatedApiKey = apiKey;
            _lastAuthenticatedAt = DateTime.UtcNow;

            // Persist last-auth fallback in Redis so it survives deployments
            try
            {
                var lastAuthJson = JsonSerializer.Serialize(new LastAuthData { ApiKey = apiKey, StoredAt = DateTime.UtcNow });
                _cache.SetString(LastAuthKey, lastAuthJson, new DistributedCacheEntryOptions
                {
                    SlidingExpiration = _lastAuthKeyTimeout
                });
            }
            catch (Exception lastAuthEx)
            {
                _logger.LogWarning(lastAuthEx, "Failed to persist last-auth key to Redis");
            }

            _logger.LogDebug("Stored API key for MCP session {SessionId} in Redis", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, falling back to in-memory for session {SessionId}", sessionId);
            _redisAvailable = false;

            _fallbackStore.AddOrUpdate(
                sessionId,
                _ => data,
                (_, _) => data);

            _lastAuthenticatedApiKey = apiKey;
            _lastAuthenticatedAt = DateTime.UtcNow;
        }
    }

    public string? GetApiKey(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        try
        {
            var json = _cache.GetString(KeyPrefix + sessionId);
            if (json != null)
            {
                var data = JsonSerializer.Deserialize<SessionData>(json);
                if (data?.ApiKey != null)
                {
                    data.LastActivity = DateTime.UtcNow;
                    var updatedJson = JsonSerializer.Serialize(data);
                    var options = new DistributedCacheEntryOptions
                    {
                        SlidingExpiration = _sessionTimeout
                    };
                    _cache.SetString(KeyPrefix + sessionId, updatedJson, options);
                    _redisAvailable = true;

                    return data.ApiKey;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable during GetApiKey for session {SessionId}, checking fallback", sessionId);
            _redisAvailable = false;
        }

        if (_fallbackStore.TryGetValue(sessionId, out var fallbackData))
        {
            fallbackData.LastActivity = DateTime.UtcNow;
            return fallbackData.ApiKey;
        }

        return null;
    }

    public void RemoveSession(string sessionId)
    {
        try
        {
            _cache.Remove(KeyPrefix + sessionId);
            _logger.LogDebug("Removed MCP session {SessionId} from Redis", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable during RemoveSession for {SessionId}", sessionId);
        }

        _fallbackStore.TryRemove(sessionId, out _);
    }

    public void CleanupExpiredSessions()
    {
        var cutoff = DateTime.UtcNow - _sessionTimeout;
        var expiredSessions = _fallbackStore
            .Where(kvp => kvp.Value.LastActivity < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in expiredSessions)
        {
            _fallbackStore.TryRemove(sessionId, out _);
        }

        if (expiredSessions.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired in-memory fallback sessions", expiredSessions.Count);
        }
    }

    public string? GetLastAuthenticatedApiKey()
    {
        // Check in-memory first (fast path)
        if (_lastAuthenticatedApiKey is not null &&
            DateTime.UtcNow - _lastAuthenticatedAt <= _lastAuthKeyTimeout)
        {
            return _lastAuthenticatedApiKey;
        }

        // Fall back to Redis (survives deployments)
        try
        {
            var json = _cache.GetString(LastAuthKey);
            if (json != null)
            {
                var data = JsonSerializer.Deserialize<LastAuthData>(json);
                if (data?.ApiKey != null && DateTime.UtcNow - data.StoredAt <= _lastAuthKeyTimeout)
                {
                    // Repopulate in-memory cache
                    _lastAuthenticatedApiKey = data.ApiKey;
                    _lastAuthenticatedAt = data.StoredAt;
                    _logger.LogInformation("Recovered last-auth key from Redis (stored {Age} ago)",
                        DateTime.UtcNow - data.StoredAt);
                    return data.ApiKey;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read last-auth key from Redis");
        }

        if (_lastAuthenticatedApiKey is not null)
        {
            _logger.LogDebug("Last authenticated API key is stale ({Age} old), ignoring",
                DateTime.UtcNow - _lastAuthenticatedAt);
        }

        return null;
    }

    public void StoreFingerprint(string fingerprint, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(sessionId))
            return;

        try
        {
            var options = new DistributedCacheEntryOptions
            {
                SlidingExpiration = _sessionTimeout
            };
            _cache.SetString(FingerprintPrefix + fingerprint, sessionId, options);
            _logger.LogDebug("Stored fingerprint mapping for session {SessionId} in Redis", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable, storing fingerprint in memory for session {SessionId}", sessionId);
            _fallbackStore.AddOrUpdate(
                FingerprintPrefix + fingerprint,
                _ => new SessionData { ApiKey = sessionId, LastActivity = DateTime.UtcNow },
                (_, _) => new SessionData { ApiKey = sessionId, LastActivity = DateTime.UtcNow });
        }
    }

    public string? GetSessionByFingerprint(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            return null;

        try
        {
            var sessionId = _cache.GetString(FingerprintPrefix + fingerprint);
            if (!string.IsNullOrWhiteSpace(sessionId))
                return sessionId;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Redis unavailable during GetSessionByFingerprint, checking fallback");
        }

        // Check in-memory fallback
        if (_fallbackStore.TryGetValue(FingerprintPrefix + fingerprint, out var fallbackData))
            return fallbackData.ApiKey; // ApiKey field reused to store sessionId for fingerprint entries

        return null;
    }

    public bool IsRedisAvailable => _redisAvailable;

    public int FallbackSessionCount => _fallbackStore.Count;

    private class SessionData
    {
        public string? ApiKey { get; set; }
        public DateTime LastActivity { get; set; }
    }

    private class LastAuthData
    {
        public string? ApiKey { get; set; }
        public DateTime StoredAt { get; set; }
    }
}
