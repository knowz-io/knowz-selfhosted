using Knowz.MCP.Services;
using Knowz.MCP.Services.Proxy;

namespace Knowz.MCP.Config;

/// <summary>
/// DI composition for MCP server (proxy or self-hosted mode).
/// </summary>
public static class McpStartupExtensions
{
    /// <summary>
    /// Registers backend services based on MCP:BackendMode configuration.
    /// "proxy" (default): forwards tool calls to Knowz Platform API via McpApiProxyService.
    /// "selfhosted": maps tool calls to individual self-hosted REST API endpoints.
    /// Call this after AddMcpServer() and before Build().
    /// </summary>
    public static IServiceCollection AddMcpBackend(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUrl = configuration["Knowz:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("Knowz:BaseUrl is required");

        var backendMode = configuration["MCP:BackendMode"] ?? "proxy";

        if (backendMode.Equals("selfhosted", StringComparison.OrdinalIgnoreCase))
        {
            // Self-hosted mode: map tools directly to REST API endpoints
            services.AddScoped<IToolBackend, SelfHostedToolBackend>();
        }
        else
        {
            // Proxy mode (default): forward all tool calls through McpApiProxyService
            services.AddScoped<IMcpApiProxyService, McpApiProxyService>();
            services.AddScoped<IToolBackend, ProxyToolBackend>();
        }

        return services;
    }
}
