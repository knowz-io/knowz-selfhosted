namespace Knowz.MCP.Services;

public interface IMcpSSOService
{
    /// <summary>Returns enabled SSO providers based on platform config.</summary>
    List<McpSSOProvider> GetEnabledProviders();

    /// <summary>Starts the OIDC flow: generates authorize URL, stores state.</summary>
    Task<McpSSOStartResult> StartSSOFlowAsync(string provider, string requestId, string callbackUrl);

    /// <summary>Handles OIDC callback: validates token, resolves email to API key via platform.</summary>
    Task<McpSSOCallbackResult> HandleSSOCallbackAsync(string code, string state);
}

public class McpSSOProvider
{
    public string Provider { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class McpSSOStartResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? AuthorizationUrl { get; set; }
}

public class McpSSOCallbackResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RequestId { get; set; }
    public string? ApiKey { get; set; }
    public string? Email { get; set; }
}
