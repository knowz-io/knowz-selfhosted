using Knowz.MCP.Services.Session;

namespace Knowz.MCP.Endpoints;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapMethods("/health", new[] { "GET", "HEAD" }, (IMcpSessionStore sessionStore) =>
        {
            var redisAvailable = sessionStore is RedisMcpSessionStore redisStore && redisStore.IsRedisAvailable;
            var fallbackCount = sessionStore is RedisMcpSessionStore rs ? rs.FallbackSessionCount : -1;

            return Results.Ok(new
            {
                status = "healthy",
                mode = "proxy",
                version = "2.0.0",
                sessionStore = redisAvailable ? "redis" : "in-memory-fallback",
                fallbackSessionCount = fallbackCount
            });
        });

        return app;
    }
}
