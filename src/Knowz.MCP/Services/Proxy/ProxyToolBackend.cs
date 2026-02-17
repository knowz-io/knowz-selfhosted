using System.Text.Json;

namespace Knowz.MCP.Services.Proxy;

/// <summary>
/// Proxy mode tool backend: forwards tool calls to the Knowz Platform API.
/// Wraps the existing McpApiProxyService.
/// </summary>
public class ProxyToolBackend : IToolBackend
{
    private readonly IMcpApiProxyService _proxyService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<ProxyToolBackend> _logger;

    public ProxyToolBackend(
        IMcpApiProxyService proxyService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<ProxyToolBackend> logger)
    {
        _proxyService = proxyService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<string> ExecuteToolAsync(
        string toolName,
        Dictionary<string, object> arguments,
        CancellationToken cancellationToken = default)
    {
        var context = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No HttpContext available");

        var apiKey = context.Items["ApiKey"] as string
            ?? throw new InvalidOperationException("No API key available for proxy mode");

        _logger.LogInformation("ProxyToolBackend: Forwarding tool {ToolName} to Knowz API", toolName);

        var result = await _proxyService.ProxyRequestAsync<Dictionary<string, object>, object>(
            $"tools/call",
            new Dictionary<string, object>
            {
                ["name"] = toolName,
                ["arguments"] = arguments
            },
            apiKey,
            cancellationToken);

        return result != null
            ? JsonSerializer.Serialize(result)
            : "{}";
    }
}
