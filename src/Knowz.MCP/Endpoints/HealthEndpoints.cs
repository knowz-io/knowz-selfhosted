namespace Knowz.MCP.Endpoints;

public static class HealthEndpoints
{
    public static WebApplication MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () =>
        {
            return Results.Ok(new
            {
                status = "healthy",
                mode = "proxy",
                version = "2.0.0"
            });
        });

        return app;
    }
}
