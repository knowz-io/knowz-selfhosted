namespace Knowz.Core.Configuration;

/// <summary>
/// Configuration options specific to self-hosted mode.
/// Bound from the "SelfHosted" configuration section.
/// </summary>
public class SelfHostedOptions
{
    public const string SectionName = "SelfHosted";

    /// <summary>
    /// Tenant ID for scoping all queries. Self-hosted is single-tenant.
    /// Default: 00000000-0000-0000-0000-000000000001
    /// </summary>
    public Guid TenantId { get; set; } = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>
    /// MCP server name returned in the initialize handshake.
    /// </summary>
    public string ServerName { get; set; } = "knowz-mcp-selfhosted";

    /// <summary>
    /// If set, all requests must include this API key.
    /// If empty/null, no authentication is required (single-user deployment).
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Allowed CORS origins. If null or empty, all origins are allowed (backward compatible).
    /// Example: ["http://localhost:5173", "https://my-knowz.example.com"]
    /// </summary>
    public string[]? AllowedOrigins { get; set; }

    // --- SuperAdmin defaults (seeded on first run) ---

    /// <summary>
    /// Default SuperAdmin username. Created on first startup if no SuperAdmin user exists.
    /// </summary>
    public string SuperAdminUsername { get; set; } = "admin";

    /// <summary>
    /// Default SuperAdmin password. Created on first startup if no SuperAdmin user exists.
    /// MUST be changed in production.
    /// </summary>
    public string SuperAdminPassword { get; set; } = "changeme";

    // --- JWT Configuration ---

    /// <summary>
    /// Secret key for signing JWT tokens. MUST be at least 32 characters.
    /// Set via appsettings or environment variable SelfHosted__JwtSecret.
    /// </summary>
    public string JwtSecret { get; set; } = "";

    /// <summary>
    /// JWT token expiration in minutes. Default: 1440 (24 hours).
    /// </summary>
    public int JwtExpirationMinutes { get; set; } = 1440;

    /// <summary>
    /// JWT issuer claim value.
    /// </summary>
    public string JwtIssuer { get; set; } = "knowz-selfhosted";

    // --- Portability Export Configuration ---

    /// <summary>
    /// Include binary file content in exports (Base64 encoded).
    /// Default: false (metadata-only exports).
    /// </summary>
    public bool IncludeBinaryContent { get; set; }

    /// <summary>
    /// Maximum file size (in MB) to include as Base64 in exports.
    /// Files larger than this are excluded from binary export.
    /// Default: 50MB.
    /// </summary>
    public int MaxBinaryFileSizeMB { get; set; } = 50;
}
