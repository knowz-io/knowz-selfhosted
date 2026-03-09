using System.Collections.Concurrent;

namespace Knowz.MCP.Services.Session;

/// <summary>
/// Thread-safe storage for MCP session data, persisting across HTTP requests within a session.
/// </summary>
public interface IMcpSessionStore
{
    void StoreApiKey(string sessionId, string apiKey);
    string? GetApiKey(string sessionId);
    void RemoveSession(string sessionId);
    void CleanupExpiredSessions();

    /// <summary>
    /// Stores a mapping from client fingerprint (IP+User-Agent) to session ID.
    /// Used to recover sessions when clients send neither Mcp-Session-Id nor cookies.
    /// </summary>
    void StoreFingerprint(string fingerprint, string sessionId);

    /// <summary>
    /// Retrieves the most recently stored session ID for a client fingerprint.
    /// Returns null if no mapping exists or the mapping has expired.
    /// </summary>
    string? GetSessionByFingerprint(string fingerprint);
}

public class McpSessionStore : IMcpSessionStore
{
    private readonly ConcurrentDictionary<string, SessionData> _sessions = new();
    private readonly ConcurrentDictionary<string, FingerprintData> _fingerprints = new();
    private readonly TimeSpan _sessionTimeout = TimeSpan.FromHours(24);
    private readonly ILogger<McpSessionStore> _logger;

    public McpSessionStore(ILogger<McpSessionStore> logger)
    {
        _logger = logger;
    }

    public void StoreApiKey(string sessionId, string apiKey)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(apiKey))
            return;

        _sessions.AddOrUpdate(
            sessionId,
            _ => new SessionData { ApiKey = apiKey, LastActivity = DateTime.UtcNow },
            (_, existing) =>
            {
                existing.ApiKey = apiKey;
                existing.LastActivity = DateTime.UtcNow;
                return existing;
            });

        _logger.LogDebug("Stored API key for MCP session {SessionId}", sessionId);
    }

    public string? GetApiKey(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        if (_sessions.TryGetValue(sessionId, out var data))
        {
            data.LastActivity = DateTime.UtcNow;
            return data.ApiKey;
        }

        return null;
    }

    public void RemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out _))
        {
            _logger.LogDebug("Removed MCP session {SessionId}", sessionId);
        }
    }

    public void CleanupExpiredSessions()
    {
        var cutoff = DateTime.UtcNow - _sessionTimeout;
        var expiredSessions = _sessions
            .Where(kvp => kvp.Value.LastActivity < cutoff)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var sessionId in expiredSessions)
        {
            RemoveSession(sessionId);
        }

        if (expiredSessions.Count > 0)
        {
            _logger.LogInformation("Cleaned up {Count} expired MCP sessions", expiredSessions.Count);
        }
    }

    public void StoreFingerprint(string fingerprint, string sessionId)
    {
        if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(sessionId))
            return;

        _fingerprints.AddOrUpdate(
            fingerprint,
            _ => new FingerprintData { SessionId = sessionId, StoredAt = DateTime.UtcNow },
            (_, _) => new FingerprintData { SessionId = sessionId, StoredAt = DateTime.UtcNow });

        _logger.LogDebug("Stored fingerprint mapping for session {SessionId}", sessionId);
    }

    public string? GetSessionByFingerprint(string fingerprint)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
            return null;

        if (_fingerprints.TryGetValue(fingerprint, out var data))
        {
            // Expire fingerprints along with sessions
            if (DateTime.UtcNow - data.StoredAt > _sessionTimeout)
            {
                _fingerprints.TryRemove(fingerprint, out _);
                return null;
            }
            return data.SessionId;
        }

        return null;
    }

    private class SessionData
    {
        public string? ApiKey { get; set; }
        public DateTime LastActivity { get; set; }
    }

    private class FingerprintData
    {
        public string SessionId { get; set; } = "";
        public DateTime StoredAt { get; set; }
    }
}
