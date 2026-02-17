namespace Knowz.MCP.Services.Session;

/// <summary>
/// Background service that periodically logs active session metrics and cleans up
/// any in-memory fallback sessions. Redis handles TTL-based expiration natively.
/// </summary>
public class SessionCleanupBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SessionCleanupBackgroundService> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(30);

    public SessionCleanupBackgroundService(IServiceProvider serviceProvider, ILogger<SessionCleanupBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MCP session cleanup service started (interval: {Interval})", _interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                var sessionStore = _serviceProvider.GetRequiredService<IMcpSessionStore>();

                sessionStore.CleanupExpiredSessions();

                if (sessionStore is RedisMcpSessionStore redisStore)
                {
                    _logger.LogInformation(
                        "MCP session metrics -- Redis available: {RedisAvailable}, In-memory fallback sessions: {FallbackCount}",
                        redisStore.IsRedisAvailable,
                        redisStore.FallbackSessionCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during MCP session cleanup");
            }
        }

        _logger.LogInformation("MCP session cleanup service stopped");
    }
}
