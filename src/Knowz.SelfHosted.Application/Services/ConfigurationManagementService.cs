using Knowz.Core.Entities;
using Knowz.Core.Interfaces;
using Knowz.SelfHosted.Infrastructure.Data;
using Knowz.SelfHosted.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Knowz.SelfHosted.Application.Services;

public class ConfigurationManagementService : IConfigurationManagementService
{
    private readonly SelfHostedDbContext _db;
    private readonly IDataProtector _dataProtector;
    private readonly DatabaseConfigurationProvider? _configProvider;
    private readonly ILogger<ConfigurationManagementService> _logger;
    private readonly IConfiguration _configuration;

    // Track restart-required changes since startup
    private static readonly DateTime _startupTime = DateTime.UtcNow;
    private static readonly List<string> _restartReasons = new();
    private static readonly object _restartLock = new();

    public ConfigurationManagementService(
        SelfHostedDbContext db,
        IDataProtectionProvider dataProtectionProvider,
        DatabaseConfigurationProvider? configProvider,
        ILogger<ConfigurationManagementService> logger,
        IConfiguration configuration)
    {
        _db = db;
        _dataProtector = dataProtectionProvider.CreateProtector("Knowz.SelfHosted.SystemConfiguration");
        _configProvider = configProvider;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<List<ConfigCategoryDto>> GetAllCategoriesAsync()
    {
        var dbEntries = await _db.SystemConfigurations.ToListAsync();
        var result = new List<ConfigCategoryDto>();

        foreach (var (categoryName, schema) in CategorySchemas)
        {
            result.Add(BuildCategoryDto(categoryName, schema, dbEntries));
        }

        return result;
    }

    public async Task<ConfigCategoryDto?> GetCategoryAsync(string category)
    {
        if (!CategorySchemas.TryGetValue(category, out var schema))
            return null;

        var dbEntries = await _db.SystemConfigurations
            .Where(sc => sc.Category == category)
            .ToListAsync();

        return BuildCategoryDto(category, schema, dbEntries);
    }

    public async Task<ConfigUpdateResult> UpdateCategoryAsync(
        string category, List<ConfigEntryUpdateDto> entries, string modifiedBy)
    {
        if (!CategorySchemas.TryGetValue(category, out var schema))
        {
            return new ConfigUpdateResult
            {
                Success = false,
                Errors = new List<string> { $"Unknown category: {category}" }
            };
        }

        var errors = new List<string>();
        foreach (var entry in entries)
        {
            if (!schema.Keys.ContainsKey(entry.Key))
            {
                errors.Add($"Unknown key '{entry.Key}' in category '{category}'");
            }
        }

        if (errors.Count > 0)
        {
            return new ConfigUpdateResult { Success = false, Errors = errors };
        }

        var dbEntries = await _db.SystemConfigurations
            .Where(sc => sc.Category == category)
            .ToListAsync();

        var restartRequired = false;
        var entriesUpdated = 0;

        foreach (var entry in entries)
        {
            var keySchema = schema.Keys[entry.Key];

            // Sentinel detection: if value is all asterisks, keep existing
            if (IsKeepExistingSentinel(entry.Value))
                continue;

            var existing = dbEntries.FirstOrDefault(e => e.Key == entry.Key);

            string? encryptedValue = null;
            if (entry.Value is not null)
            {
                encryptedValue = _dataProtector.Protect(entry.Value);
            }

            if (existing is not null)
            {
                existing.EncryptedValue = encryptedValue;
                existing.IsSecret = keySchema.IsSecret;
                existing.RequiresRestart = keySchema.RequiresRestart;
                existing.Description = keySchema.Description;
                existing.LastModifiedAt = DateTime.UtcNow;
                existing.LastModifiedBy = modifiedBy;
            }
            else
            {
                _db.SystemConfigurations.Add(new SystemConfiguration
                {
                    Category = category,
                    Key = entry.Key,
                    EncryptedValue = encryptedValue,
                    IsSecret = keySchema.IsSecret,
                    RequiresRestart = keySchema.RequiresRestart,
                    Description = keySchema.Description,
                    LastModifiedAt = DateTime.UtcNow,
                    LastModifiedBy = modifiedBy
                });
            }

            if (keySchema.RequiresRestart)
                restartRequired = true;

            entriesUpdated++;
        }

        await _db.SaveChangesAsync();

        if (restartRequired)
        {
            lock (_restartLock)
            {
                var reason = $"Category '{category}' updated at {DateTime.UtcNow:u}";
                if (!_restartReasons.Contains(reason))
                    _restartReasons.Add(reason);
            }
        }

        // Trigger config reload
        try
        {
            _configProvider?.Reload();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to reload configuration provider after saving category {Category}", category);
        }

        return new ConfigUpdateResult
        {
            Success = true,
            RestartRequired = restartRequired,
            EntriesUpdated = entriesUpdated
        };
    }

    public async Task<ServiceHealthResult> TestConnectionAsync(string category)
    {
        if (!CategorySchemas.TryGetValue(category, out var schema))
        {
            return new ServiceHealthResult
            {
                Category = category,
                DisplayName = category,
                IsHealthy = false,
                Status = "Unknown category"
            };
        }

        var dbEntries = await _db.SystemConfigurations
            .Where(sc => sc.Category == category)
            .ToListAsync();

        var result = new ServiceHealthResult
        {
            Category = category,
            DisplayName = schema.DisplayName
        };

        try
        {
            switch (category)
            {
                case "ConnectionStrings":
                    await TestSqlConnectionAsync(dbEntries, result);
                    break;
                case "AzureOpenAI":
                    await TestAzureOpenAIAsync(dbEntries, result);
                    break;
                case "AzureAISearch":
                    await TestAzureAISearchAsync(dbEntries, result);
                    break;
                case "Storage":
                    await TestStorageAsync(dbEntries, result);
                    break;
                case "SSO":
                    await TestSSOConnectionAsync(dbEntries, result);
                    break;
                default:
                    result.IsHealthy = true;
                    result.Status = "No connectivity test available";
                    break;
            }
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.Status = $"Error: {ex.Message}";
        }

        return result;
    }

    public async Task<List<ServiceHealthResult>> TestAllConnectionsAsync()
    {
        var results = new List<ServiceHealthResult>();
        foreach (var category in CategorySchemas.Keys)
        {
            results.Add(await TestConnectionAsync(category));
        }
        return results;
    }

    public async Task SeedFromConfigurationAsync(IConfiguration configuration)
    {
        var hasEntries = await _db.SystemConfigurations.AnyAsync();
        if (hasEntries)
        {
            _logger.LogInformation("SystemConfigurations table already has entries, skipping seed");
            return;
        }

        _logger.LogInformation("Seeding SystemConfigurations from current configuration");

        foreach (var (categoryName, schema) in CategorySchemas)
        {
            foreach (var (keyName, keySchema) in schema.Keys)
            {
                var configKey = $"{categoryName}:{keyName}";
                var value = configuration[configKey];

                string? encryptedValue = null;
                if (value is not null)
                {
                    encryptedValue = _dataProtector.Protect(value);
                }

                _db.SystemConfigurations.Add(new SystemConfiguration
                {
                    Category = categoryName,
                    Key = keyName,
                    EncryptedValue = encryptedValue,
                    IsSecret = keySchema.IsSecret,
                    RequiresRestart = keySchema.RequiresRestart,
                    Description = keySchema.Description,
                    LastModifiedAt = DateTime.UtcNow,
                    LastModifiedBy = "system-seed"
                });
            }
        }

        await _db.SaveChangesAsync();
        _logger.LogInformation("Seeded {Count} configuration entries", CategorySchemas.Values.Sum(s => s.Keys.Count));
    }

    public DeploymentStatusDto GetDeploymentStatus()
    {
        List<string> reasons;
        lock (_restartLock)
        {
            reasons = new List<string>(_restartReasons);
        }

        return new DeploymentStatusDto
        {
            Mode = "Direct",
            Version = typeof(ConfigurationManagementService).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            StartupTime = _startupTime,
            RestartRequired = reasons.Count > 0,
            RestartReasons = reasons
        };
    }

    // --- Private helpers ---

    private ConfigCategoryDto BuildCategoryDto(string categoryName, CategorySchema schema, List<SystemConfiguration> dbEntries)
    {
        var categoryRequiresRestart = schema.Keys.Values.Any(k => k.RequiresRestart);
        var kvEnabled = _configuration.GetValue<bool>("AzureKeyVault:Enabled");
        var kvUri = _configuration["AzureKeyVault:VaultUri"];
        var isKeyVaultConfigured = kvEnabled && !string.IsNullOrWhiteSpace(kvUri);

        var entries = schema.Keys.Select(kvp =>
        {
            var dbEntry = dbEntries.FirstOrDefault(e => e.Category == categoryName && e.Key == kvp.Key);
            string? decryptedValue = null;
            if (dbEntry?.EncryptedValue is not null)
            {
                try
                {
                    decryptedValue = _dataProtector.Unprotect(dbEntry.EncryptedValue);
                }
                catch
                {
                    decryptedValue = null;
                }
            }

            // Determine source
            string? source = null;
            if (decryptedValue is not null)
            {
                source = "database";
            }
            else
            {
                var configKey = $"{categoryName}:{kvp.Key}";
                var configValue = _configuration[configKey];
                if (configValue is not null)
                {
                    source = isKeyVaultConfigured ? "keyvault" : "environment";
                }
            }

            return new ConfigEntryDto
            {
                Key = kvp.Key,
                Value = MaskValue(decryptedValue, kvp.Value.IsSecret),
                IsSecret = kvp.Value.IsSecret,
                RequiresRestart = kvp.Value.RequiresRestart,
                Description = kvp.Value.Description,
                IsSet = decryptedValue is not null,
                LastModifiedAt = dbEntry?.LastModifiedAt,
                LastModifiedBy = dbEntry?.LastModifiedBy,
                Source = source
            };
        }).ToList();

        return new ConfigCategoryDto
        {
            Category = categoryName,
            DisplayName = schema.DisplayName,
            Description = schema.Description,
            RequiresRestart = categoryRequiresRestart,
            Entries = entries
        };
    }

    internal static string? MaskValue(string? value, bool isSecret)
    {
        if (!isSecret)
            return value;

        if (string.IsNullOrEmpty(value))
            return null;

        if (value.Length <= 4)
            return "****";

        return "****" + value[^4..];
    }

    internal static bool IsKeepExistingSentinel(string? value)
    {
        return value is not null && value.Length > 0 && value.All(c => c == '*');
    }

    private async Task TestSqlConnectionAsync(List<SystemConfiguration> entries, ServiceHealthResult result)
    {
        var connEntry = entries.FirstOrDefault(e => e.Key == "McpDb");
        if (connEntry?.EncryptedValue is null)
        {
            result.IsHealthy = false;
            result.Status = "Not Configured";
            return;
        }

        var connectionString = _dataProtector.Unprotect(connEntry.EncryptedValue);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT 1";
        await cmd.ExecuteScalarAsync();

        sw.Stop();
        result.IsHealthy = true;
        result.Status = "Connected";
        result.LatencyMs = (int)sw.ElapsedMilliseconds;
    }

    private Task TestAzureOpenAIAsync(List<SystemConfiguration> entries, ServiceHealthResult result)
    {
        var endpoint = entries.FirstOrDefault(e => e.Key == "Endpoint");
        var apiKey = entries.FirstOrDefault(e => e.Key == "ApiKey");

        if (endpoint?.EncryptedValue is null || apiKey?.EncryptedValue is null)
        {
            result.IsHealthy = false;
            result.Status = "Not Configured";
            return Task.CompletedTask;
        }

        // Basic validation - verify endpoint format
        var endpointValue = _dataProtector.Unprotect(endpoint.EncryptedValue);
        if (Uri.TryCreate(endpointValue, UriKind.Absolute, out _))
        {
            result.IsHealthy = true;
            result.Status = "Configured";
        }
        else
        {
            result.IsHealthy = false;
            result.Status = "Error: Invalid endpoint URL";
        }

        return Task.CompletedTask;
    }

    private Task TestAzureAISearchAsync(List<SystemConfiguration> entries, ServiceHealthResult result)
    {
        var endpoint = entries.FirstOrDefault(e => e.Key == "Endpoint");
        var apiKey = entries.FirstOrDefault(e => e.Key == "ApiKey");

        if (endpoint?.EncryptedValue is null || apiKey?.EncryptedValue is null)
        {
            result.IsHealthy = false;
            result.Status = "Not Configured";
            return Task.CompletedTask;
        }

        var endpointValue = _dataProtector.Unprotect(endpoint.EncryptedValue);
        if (Uri.TryCreate(endpointValue, UriKind.Absolute, out _))
        {
            result.IsHealthy = true;
            result.Status = "Configured";
        }
        else
        {
            result.IsHealthy = false;
            result.Status = "Error: Invalid endpoint URL";
        }

        return Task.CompletedTask;
    }

    private Task TestStorageAsync(List<SystemConfiguration> entries, ServiceHealthResult result)
    {
        var provider = entries.FirstOrDefault(e => e.Key == "Provider");
        if (provider?.EncryptedValue is null)
        {
            result.IsHealthy = false;
            result.Status = "Not Configured";
            return Task.CompletedTask;
        }

        var providerValue = _dataProtector.Unprotect(provider.EncryptedValue);
        if (providerValue == "LocalFileSystem")
        {
            var rootPath = entries.FirstOrDefault(e => e.Key == "Local:RootPath");
            if (rootPath?.EncryptedValue is not null)
            {
                var path = _dataProtector.Unprotect(rootPath.EncryptedValue);
                if (Directory.Exists(path))
                {
                    result.IsHealthy = true;
                    result.Status = "Connected";
                }
                else
                {
                    result.IsHealthy = false;
                    result.Status = $"Error: Directory not found: {path}";
                }
            }
            else
            {
                result.IsHealthy = false;
                result.Status = "Not Configured";
            }
        }
        else
        {
            var connString = entries.FirstOrDefault(e => e.Key == "Azure:ConnectionString");
            if (connString?.EncryptedValue is null)
            {
                result.IsHealthy = false;
                result.Status = "Not Configured";
            }
            else
            {
                result.IsHealthy = true;
                result.Status = "Configured";
            }
        }

        return Task.CompletedTask;
    }

    private async Task TestSSOConnectionAsync(List<SystemConfiguration> dbEntries, ServiceHealthResult result)
    {
        var isEnabled = GetDecryptedValue(dbEntries, "Enabled");
        if (isEnabled?.ToLower() != "true")
        {
            result.IsHealthy = true;
            result.Status = "SSO is disabled";
            return;
        }

        var errors = new List<string>();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        // Test Microsoft OIDC discovery (mode-aware)
        var msClientId = GetDecryptedValue(dbEntries, "Microsoft:ClientId");
        if (!string.IsNullOrEmpty(msClientId))
        {
            var msClientSecret = GetDecryptedValue(dbEntries, "Microsoft:ClientSecret");
            var directoryTenantId = GetDecryptedValue(dbEntries, "Microsoft:DirectoryTenantId");

            var hasSecret = !string.IsNullOrEmpty(msClientSecret);
            var tenantIdRaw = directoryTenantId ?? "";
            var tenantIds = tenantIdRaw
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(s => Guid.TryParse(s, out _))
                .Select(Guid.Parse)
                .Distinct()
                .ToList();

            string mode;
            if (hasSecret)
            {
                mode = "Confidential Client";
            }
            else if (tenantIds.Count > 0)
            {
                mode = "PKCE Public Client";
            }
            else
            {
                errors.Add("Microsoft SSO: ClientId set but no ClientSecret or DirectoryTenantId -- cannot determine mode");
                mode = "Incomplete";
            }

            // Validate tenant ID format if provided
            if (!string.IsNullOrEmpty(directoryTenantId))
            {
                var parts = directoryTenantId.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                var invalidParts = parts.Where(p => !Guid.TryParse(p, out _)).ToList();
                if (invalidParts.Count > 0)
                    errors.Add($"Microsoft SSO: Invalid tenant ID(s): {string.Join(", ", invalidParts)}");
            }

            // Test OIDC discovery
            try
            {
                string authority;
                if (tenantIds.Count == 1)
                    authority = $"https://login.microsoftonline.com/{tenantIds[0]}/v2.0";
                else
                    authority = "https://login.microsoftonline.com/common/v2.0";

                var response = await httpClient.GetAsync($"{authority}/.well-known/openid-configuration");
                if (!response.IsSuccessStatusCode)
                    errors.Add($"Microsoft OIDC discovery failed: HTTP {response.StatusCode}");
            }
            catch (Exception ex)
            {
                errors.Add($"Microsoft OIDC discovery error: {ex.Message}");
            }

            if (errors.Count == 0)
                result.Status = $"SSO providers reachable (Microsoft: {mode}, {tenantIds.Count} tenant(s))";
        }

        // Test Google OIDC discovery
        var googleClientId = GetDecryptedValue(dbEntries, "Google:ClientId");
        if (!string.IsNullOrEmpty(googleClientId))
        {
            try
            {
                var response = await httpClient.GetAsync(
                    "https://accounts.google.com/.well-known/openid-configuration");
                if (!response.IsSuccessStatusCode)
                    errors.Add($"Google OIDC discovery failed: HTTP {response.StatusCode}");
            }
            catch (Exception ex)
            {
                errors.Add($"Google OIDC discovery error: {ex.Message}");
            }
        }

        if (errors.Count == 0)
        {
            result.IsHealthy = true;
            // Keep mode-aware status if already set, otherwise use generic
            if (string.IsNullOrEmpty(result.Status))
                result.Status = "SSO providers reachable";
        }
        else
        {
            result.IsHealthy = false;
            result.Status = string.Join("; ", errors);
        }
    }

    private string? GetDecryptedValue(List<SystemConfiguration> entries, string key)
    {
        var entry = entries.FirstOrDefault(e => e.Key == key);
        if (entry?.EncryptedValue is null) return null;
        try
        {
            return _dataProtector.Unprotect(entry.EncryptedValue);
        }
        catch
        {
            return null;
        }
    }

    // --- Category Schema Registry ---

    internal class CategorySchema
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public Dictionary<string, KeySchema> Keys { get; set; } = new();
    }

    internal class KeySchema
    {
        public bool IsSecret { get; set; }
        public bool RequiresRestart { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    internal static readonly Dictionary<string, CategorySchema> CategorySchemas = new()
    {
        ["ConnectionStrings"] = new CategorySchema
        {
            DisplayName = "Database",
            Description = "SQL Server connection configuration",
            Keys = new Dictionary<string, KeySchema>
            {
                ["McpDb"] = new() { IsSecret = true, RequiresRestart = true, Description = "SQL Server connection string" }
            }
        },
        ["AzureOpenAI"] = new CategorySchema
        {
            DisplayName = "Azure OpenAI",
            Description = "AI chat and embedding service configuration",
            Keys = new Dictionary<string, KeySchema>
            {
                ["Endpoint"] = new() { IsSecret = false, RequiresRestart = true, Description = "Azure OpenAI endpoint URL" },
                ["ApiKey"] = new() { IsSecret = true, RequiresRestart = true, Description = "Azure OpenAI API key" },
                ["DeploymentName"] = new() { IsSecret = false, RequiresRestart = true, Description = "Chat model deployment name (e.g., gpt-4o)" },
                ["EmbeddingDeploymentName"] = new() { IsSecret = false, RequiresRestart = true, Description = "Embedding model deployment name (e.g., text-embedding-3-small)" }
            }
        },
        ["AzureAISearch"] = new CategorySchema
        {
            DisplayName = "Azure AI Search",
            Description = "Vector search and knowledge indexing configuration",
            Keys = new Dictionary<string, KeySchema>
            {
                ["Endpoint"] = new() { IsSecret = false, RequiresRestart = true, Description = "Azure AI Search endpoint URL" },
                ["ApiKey"] = new() { IsSecret = true, RequiresRestart = true, Description = "Azure AI Search API key" },
                ["IndexName"] = new() { IsSecret = false, RequiresRestart = true, Description = "Search index name (e.g., knowledge)" }
            }
        },
        ["Storage"] = new CategorySchema
        {
            DisplayName = "File Storage",
            Description = "File upload and storage configuration",
            Keys = new Dictionary<string, KeySchema>
            {
                ["Provider"] = new() { IsSecret = false, RequiresRestart = true, Description = "Storage provider: AzureBlob or LocalFileSystem" },
                ["Azure:ConnectionString"] = new() { IsSecret = true, RequiresRestart = true, Description = "Azure Blob Storage connection string" },
                ["Azure:ContainerName"] = new() { IsSecret = false, RequiresRestart = true, Description = "Azure Blob container name" },
                ["Local:RootPath"] = new() { IsSecret = false, RequiresRestart = true, Description = "Local file storage root directory path" }
            }
        },
        ["SelfHosted"] = new CategorySchema
        {
            DisplayName = "Authentication & Application",
            Description = "JWT authentication, API keys, and application settings",
            Keys = new Dictionary<string, KeySchema>
            {
                ["JwtSecret"] = new() { IsSecret = true, RequiresRestart = false, Description = "JWT signing secret (min 32 characters)" },
                ["JwtExpirationMinutes"] = new() { IsSecret = false, RequiresRestart = false, Description = "JWT token expiration in minutes" },
                ["JwtIssuer"] = new() { IsSecret = false, RequiresRestart = false, Description = "JWT issuer claim value" },
                ["ApiKey"] = new() { IsSecret = true, RequiresRestart = false, Description = "Legacy global API key (optional)" },
                ["EnableSwagger"] = new() { IsSecret = false, RequiresRestart = true, Description = "Enable Swagger UI" },
                ["ServerName"] = new() { IsSecret = false, RequiresRestart = false, Description = "MCP server name" },
                ["AllowedOrigins"] = new() { IsSecret = false, RequiresRestart = true, Description = "CORS allowed origins (comma-separated)" }
            }
        },
        // Note: Logging is excluded from DB config because DatabaseConfigurationProvider.Load()
        // runs before Data Protection is initialized, causing encrypted log levels to crash the logger.
        ["AzureKeyVault"] = new CategorySchema
        {
            DisplayName = "Azure Key Vault",
            Description = "Optional enterprise secret store. Secrets stored here provide defaults; values set via this admin UI take precedence.",
            Keys = new Dictionary<string, KeySchema>
            {
                ["VaultUri"] = new() { IsSecret = false, RequiresRestart = true, Description = "Key Vault URI (e.g., https://my-vault.vault.azure.net/)" },
                ["Enabled"] = new() { IsSecret = false, RequiresRestart = true, Description = "Enable Key Vault integration (true/false)" }
            }
        },
        ["SSO"] = new CategorySchema
        {
            DisplayName = "Single Sign-On (SSO)",
            Description = "Microsoft and Google SSO configuration for passwordless login",
            Keys = new Dictionary<string, KeySchema>
            {
                ["Enabled"] = new()
                {
                    IsSecret = false, RequiresRestart = false,
                    Description = "Enable SSO login buttons (true/false)"
                },
                ["AutoProvisionUsers"] = new()
                {
                    IsSecret = false, RequiresRestart = false,
                    Description = "Automatically create accounts for new SSO users (true/false)"
                },
                ["DefaultRole"] = new()
                {
                    IsSecret = false, RequiresRestart = false,
                    Description = "Default role for auto-provisioned SSO users (User, Admin, SuperAdmin)"
                },
                ["Microsoft:ClientId"] = new()
                {
                    IsSecret = false, RequiresRestart = false,
                    Description = "Microsoft OAuth App Client ID (from Azure Portal)"
                },
                ["Microsoft:ClientSecret"] = new()
                {
                    IsSecret = true, RequiresRestart = false,
                    Description = "Microsoft OAuth App Client Secret"
                },
                ["Microsoft:DirectoryTenantId"] = new()
                {
                    IsSecret = false, RequiresRestart = false,
                    Description = "Entra Directory Tenant ID(s). Single GUID for one org, or comma-separated GUIDs for multi-org (e.g., 'guid1,guid2'). Required for PKCE mode."
                },
                ["Google:ClientId"] = new()
                {
                    IsSecret = false, RequiresRestart = false,
                    Description = "Google OAuth Client ID (from Google Cloud Console)"
                },
                ["Google:ClientSecret"] = new()
                {
                    IsSecret = true, RequiresRestart = false,
                    Description = "Google OAuth Client Secret"
                },
            }
        }
    };
}
